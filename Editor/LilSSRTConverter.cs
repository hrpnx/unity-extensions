using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using lilToon;
using UnityEditor;
using UnityEngine;

namespace Hrpnx.UnityExtensions
{
    /// <summary>
    /// lilSSRT（Assets/MeltzzZ）を無改変・無参照で扱うためのブリッジ。
    /// シェーダー変換は lilToon.lilSSRTInspector の protected メソッドをリフレクションで呼び、
    /// SSRT 独自 AO プロパティは「効き」だけをホワイトリストでコピーする。
    ///
    /// 変換の補強:
    ///  - Material Variant は Unity がシェーダー差し替えを禁止するため、ルート（非バリアント）親を変換して継承させる。
    ///  - 同名重複シェーダーで lilToon の参照一致ベース変換が素通りする場合、名前ベースの fallback で差し替える。
    ///  - Fur は lilSSRT 側シェーダーが未対応（コンパイルエラー）のためスキップする。
    /// </summary>
    public static class LilSSRTConverter
    {
        private const string LilSSRTShaderToken = "lilSSRT";
        private const string FurShaderToken = "Fur";
        private const string InspectorTypeName = "lilToon.lilSSRTInspector, lilSSRT.Editor";
        private const string ConvertMethodName = "ConvertMaterialToCustomShader";
        private const string ReplaceMethodName = "ReplaceToCustomShaders";

        // CustomInspector.GetAOProperties() の集合から、texture / debug / hidden を除いた「効き」25個。
        private static readonly string[] _aoFloatProperties =
        {
            "_LilSSRTAO",
            "_LilSSRTAOStrength",
            "_LilSSRTAOContrast",
            "_LilSSRTAORadius",
            "_LilSSRTAOInvertMask",
            "_LilSSRTAOAlgorithm",
            "_LilSSRTAODepthBias",
            "_LilSSRTAOSpread",
            "_LilSSRTAOJitter",
            "_LilSSRTAOFOV",
            "_LilSSRTAOFOVAdjust",
            "_LilSSRTAODistanceFadeMin",
            "_LilSSRTAODistanceFadeMax",
            "_LilSSRTSSRTAORays",
            "_LilSSRTXeGTAOSlices",
            "_LilSSRTSSRTAOSteps",
            "_LilSSRTSSRTAOThickness",
            "_LilSSRTSSRTAOTransparentReduce",
            "_LilSSRTSSRTAOHitMode",
            "_LilSSRTAOReconstructNormal",
            "_LilSSRTAOReconstructNormalStrength",
            "_LilSSRTAODepthReject",
            "_LilSSRTAONoiseStability",
            "_LilSSRTAOHitSpread",
        };

        private const string AoColorProperty = "_LilSSRTAOColor";

        // FakeBounce（疑似バウンスライト）。スコープ A: float/color に加え、描画に必須の環境 RT 含む texture も配る。
        private static readonly string[] _fakeBounceFloatProperties =
        {
            "_FakeBounceLight",
            "_FakeBouncePreventBlowout",
            "_FakeBounceStrength",
            "_FakeBounceBlur",
            "_FakeBounceBlendMode",
            "_FakeBounceInvertMask",
            "_FakeBouncePower",
            "_FakeBounceNormalBlend",
        };

        private const string FakeBounceColorProperty = "_FakeBounceColor";

        private static readonly string[] _fakeBounceTextureProperties =
        {
            "_FakeBounceMask",
            "_FakeBounceTexDown",
            "_FakeBounceTexRight",
            "_FakeBounceTexLeft",
            "_FakeBounceTexFront",
            "_FakeBounceTexBack",
        };

        private static object _inspectorInstance;
        private static MethodInfo _convertMethod;
        private static MethodInfo _replaceMethod;
        private static bool _resolveAttempted;
        private static bool _resolveFailed;

        private static Dictionary<string, Shader> _nameToSSRT; // base lilToon シェーダー名 → lilSSRT シェーダー
        private static Dictionary<string, Shader> _ssrtToBase; // lilSSRT シェーダー名 → base lilToon シェーダー（Fur 戻し用）
        private static bool _mapAttempted;
        private static bool _mapFailed;

        /// <summary>シェーダー名で lilSSRT マテリアルか判定する。</summary>
        public static bool IsLilSSRTMaterial(Material material)
        {
            return material != null
                && material.shader != null
                && material.shader.name.Contains(LilSSRTShaderToken);
        }

        /// <summary>
        /// マテリアルを対応する lilSSRT バリアントへ変換する。
        /// Variant はルート親を変換、参照不一致は名前 fallback、Fur はスキップ。
        /// </summary>
        public static bool ConvertToLilSSRT(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            // Variant はシェーダーを持たない（親から継承）。シェーダーを所有するルート親を変換対象にする。
            Material owner = ResolveShaderOwner(material);
            if (owner == null || owner.shader == null)
            {
                return false;
            }

            // Fur は lilSSRT 側シェーダーがコンパイル不可（マテリアルエラーになる）。
            // どの開始状態でも実行後に必ず lilToon Fur（正常）へ揃える。
            if (owner.shader.name.Contains(FurShaderToken))
            {
                EnsureFurStaysLilToon(owner);
                return false;
            }

            if (IsLilSSRTMaterial(owner))
            {
                return true;
            }

            if (!EnsureMethods())
            {
                return false;
            }

            // 公式変換（参照一致するシェーダーはこれで差し替え + renderQueue 処理される）
            try
            {
                _convertMethod.Invoke(_inspectorInstance, new object[] { owner });
            }
            catch (TargetInvocationException e)
            {
                Debug.LogError(
                    $"[BulkMat] lilSSRT 変換に失敗しました ({owner.name}): {e.InnerException?.Message ?? e.Message}"
                );
                return false;
            }

            // 参照不一致（同名重複シェーダー）で変換されなかった場合は名前ベースで差し替える。
            if (
                !IsLilSSRTMaterial(owner)
                && EnsureNameMap()
                && _nameToSSRT.TryGetValue(owner.shader.name, out var target)
                && target != null
            )
            {
                owner.shader = target;
            }

            return IsLilSSRTMaterial(material);
        }

        /// <summary>
        /// 検証済みの lilSSRT 参照マテリアルから AO の「効き」25プロパティ値を対象へコピーする。
        /// テクスチャ・マスク・デバッグ・キーワードは一切触らない。
        /// </summary>
        public static void CopyAOProperties(Material reference, Material target)
        {
            if (reference == null || target == null)
            {
                return;
            }

            foreach (string prop in _aoFloatProperties)
            {
                if (reference.HasProperty(prop) && target.HasProperty(prop))
                {
                    target.SetFloat(prop, reference.GetFloat(prop));
                }
            }

            if (reference.HasProperty(AoColorProperty) && target.HasProperty(AoColorProperty))
            {
                target.SetColor(AoColorProperty, reference.GetColor(AoColorProperty));
            }
        }

        /// <summary>
        /// 参照から FakeBounce（疑似バウンスライト）の効きを対象へコピーする。
        /// 描画に必須の環境 RT（_FakeBounceTex*）含む texture も配るため、チェック ON で実際に描画される。
        /// </summary>
        public static void CopyFakeBounceProperties(Material reference, Material target)
        {
            if (reference == null || target == null)
            {
                return;
            }

            foreach (string prop in _fakeBounceFloatProperties)
            {
                if (reference.HasProperty(prop) && target.HasProperty(prop))
                {
                    target.SetFloat(prop, reference.GetFloat(prop));
                }
            }

            if (
                reference.HasProperty(FakeBounceColorProperty)
                && target.HasProperty(FakeBounceColorProperty)
            )
            {
                target.SetColor(
                    FakeBounceColorProperty,
                    reference.GetColor(FakeBounceColorProperty)
                );
            }

            foreach (string prop in _fakeBounceTextureProperties)
            {
                if (reference.HasProperty(prop) && target.HasProperty(prop))
                {
                    target.SetTexture(prop, reference.GetTexture(prop));
                }
            }
        }

        // Fur マテリアルがエラー化しないよう lilToon Fur に揃える。
        // lilSSRT Fur（壊れている）なら lilToon Fur に戻し、既に lilToon Fur ならスキップ。
        private static void EnsureFurStaysLilToon(Material owner)
        {
            if (!IsLilSSRTMaterial(owner))
            {
                Debug.LogWarning(
                    $"[BulkMat] Fur マテリアルは lilSSRT 変換をスキップします（lilSSRT の Fur シェーダーが未対応）: {owner.name}"
                );
                return;
            }

            if (
                EnsureNameMap()
                && _ssrtToBase.TryGetValue(owner.shader.name, out var lilToonFur)
                && lilToonFur != null
            )
            {
                owner.shader = lilToonFur;
                Debug.LogWarning(
                    $"[BulkMat] Fur は lilSSRT 非対応のため lilToon Fur に戻しました: {owner.name}"
                );
            }
            else
            {
                Debug.LogError(
                    $"[BulkMat] Fur ({owner.name}) を lilToon に戻せませんでした（対応する lilToon Fur シェーダーが見つかりません）。"
                );
            }
        }

        // Variant のシェーダー所有元（非バリアントのルート親）を辿る。
        private static Material ResolveShaderOwner(Material material)
        {
            int guard = 0;
            while (
                material != null && material.isVariant && material.parent != null && guard++ < 16
            )
            {
                material = material.parent;
            }
            return material;
        }

        private static bool EnsureMethods()
        {
            if (_convertMethod != null && _replaceMethod != null && _inspectorInstance != null)
            {
                return true;
            }

            if (_resolveAttempted)
            {
                return !_resolveFailed;
            }

            _resolveAttempted = true;

            Type inspectorType = Type.GetType(InspectorTypeName);
            if (inspectorType == null)
            {
                _resolveFailed = true;
                Debug.LogError(
                    $"[BulkMat] 型 '{InspectorTypeName}' を解決できません。lilSSRT (Assets/MeltzzZ) が import されているか確認してください。"
                );
                return false;
            }

            _convertMethod = FindInstanceMethod(
                inspectorType,
                ConvertMethodName,
                new[] { typeof(Material) }
            );
            _replaceMethod = FindInstanceMethod(inspectorType, ReplaceMethodName, Type.EmptyTypes);
            if (_convertMethod == null || _replaceMethod == null)
            {
                _resolveFailed = true;
                Debug.LogError(
                    $"[BulkMat] lilSSRT の変換メソッドを解決できません。lilSSRT のバージョンを確認してください。"
                );
                return false;
            }

            try
            {
                _inspectorInstance = Activator.CreateInstance(inspectorType);
            }
            catch (Exception e)
            {
                _resolveFailed = true;
                Debug.LogError($"[BulkMat] lilSSRTInspector の生成に失敗しました: {e.Message}");
                return false;
            }

            return true;
        }

        // protected な継承メソッドのため基底クラスを遡って解決する。
        private static MethodInfo FindInstanceMethod(Type type, string name, Type[] parameters)
        {
            for (Type cur = type; cur != null; cur = cur.BaseType)
            {
                var m = cur.GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    parameters,
                    null
                );
                if (m != null)
                {
                    return m;
                }
            }
            return null;
        }

        // lilShaderManager の base lilToon シェーダー名 → lilSSRT シェーダー の対応表を一度だけ構築する。
        // base/lilSSRT の各バリアントを同一フィールドで前後スナップショットして突き合わせる。
        private static bool EnsureNameMap()
        {
            if (_nameToSSRT != null)
            {
                return true;
            }

            if (_mapAttempted)
            {
                return !_mapFailed;
            }

            _mapAttempted = true;
            if (!EnsureMethods())
            {
                _mapFailed = true;
                return false;
            }

            try
            {
                var fields = typeof(lilShaderManager)
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.FieldType == typeof(Shader))
                    .ToArray();

                lilShaderManager.InitializeShaders();
                var baseShaders = fields.Select(f => (Shader)f.GetValue(null)).ToArray();

                _replaceMethod.Invoke(_inspectorInstance, null);
                var ssrtShaders = fields.Select(f => (Shader)f.GetValue(null)).ToArray();

                lilShaderManager.InitializeShaders(); // 静的フィールドを base lilToon に戻す

                var map = new Dictionary<string, Shader>();
                var reverse = new Dictionary<string, Shader>();
                for (int i = 0; i < fields.Length; i++)
                {
                    Shader b = baseShaders[i];
                    Shader s = ssrtShaders[i];
                    if (b == null || s == null || b == s)
                    {
                        continue;
                    }

                    if (!map.ContainsKey(b.name))
                    {
                        map[b.name] = s;
                    }

                    if (!reverse.ContainsKey(s.name))
                    {
                        reverse[s.name] = b;
                    }
                }

                _nameToSSRT = map;
                _ssrtToBase = reverse;
                return true;
            }
            catch (Exception e)
            {
                _mapFailed = true;
                Debug.LogError($"[BulkMat] lilSSRT 名前マップの構築に失敗しました: {e.Message}");
                return false;
            }
        }
    }
}
