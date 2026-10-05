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
    /// SSRT 独自プロパティはホワイトリストでコピーしたあと、lilSSRT 自身の同期処理でシェーダーとキーワードを確定させる。
    ///
    /// 変換の補強:
    ///  - Material Variant は Unity がシェーダー差し替えを禁止するため、ルート（非バリアント）親を変換して継承させる。
    ///  - 同名重複シェーダーで lilToon の参照一致ベース変換が素通りする場合、名前ベースの fallback で差し替える。
    /// </summary>
    public static class LilSSRTConverter
    {
        private const string LilSSRTShaderToken = "lilSSRT";
        private const string InspectorTypeName = "lilToon.lilSSRTInspector, lilSSRT.Editor";
        private const string ConvertMethodName = "ConvertMaterialToCustomShader";
        private const string ReplaceMethodName = "ReplaceToCustomShaders";
        private const string VariantsTypeName = "lilToon.SSRTMaterialVariants, lilSSRT.Editor";
        private const string MigrationTypeName =
            "lilToon.SSRTLegacyMaterialMigration, lilSSRT.Editor";
        private const string SyncMethodName = "Sync";
        private const string MigrateMethodName = "Migrate";

        // CustomInspector.GetAOProperties() の集合から、マスクテクスチャ（UV 依存でマテリアル固有）と
        // デバッグ表示を除いたもの。lilSSRT の更新でプロパティが増えたらここへ足す。
        private static readonly string[] _aoFloatProperties =
        {
            "_LilSSRTAO",
            "_LilSSRTAOStrength",
            "_LilSSRTAOContrast",
            "_LilSSRTAORadius",
            "_LilSSRTAODualRadius",
            "_LilSSRTAONearRadius",
            "_LilSSRTAONearStrength",
            "_LilSSRTAOSecondaryRadius",
            "_LilSSRTAOSecondaryStrength",
            "_LilSSRTAOTertiaryRadius",
            "_LilSSRTAOTertiaryStrength",
            "_LilSSRTAOInvertMask",
            "_LilSSRTAOMaskStrength",
            "_LilSSRTAOAlgorithm",
            "_LilSSRTAOQuality",
            "_LilSSRTAOEvaluationMode",
            "_LilSSRTAOVertexTwoPhase",
            "_LilSSRTAOVertexTessellation",
            // 品質設定の形式バージョン。参照と揃えておかないと、次に lilSSRT が同期した時（インスペクタ表示など）に
            // 旧形式からの移行とみなされ、Quality / Evaluation / Tessellation が上書きされる。
            "_LilSSRTAOVariantVersion",
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
            "_LilSSRTSSRTAOIntensity",
            "_LilSSRTSSRTAOTransparentReduce",
            "_LilSSRTSSRTAOHitMode",
            "_LilSSRTAOReconstructNormal",
            "_LilSSRTAOReconstructNormalStrength",
            "_LilSSRTAODepthReject",
            "_LilSSRTAONoiseStability",
            "_LilSSRTAOHitSpread",
        };

        private static readonly string[] _aoColorProperties =
        {
            "_LilSSRTAOColor",
            "_LilSSRTAOSecondaryColor",
            "_LilSSRTAOTertiaryColor",
        };

        // FakeBounce（疑似バウンスライト）。スコープ A: float/color に加え、描画に必須の環境 RT 含む texture も配る。
        private static readonly string[] _fakeBounceFloatProperties =
        {
            "_FakeBounceLight",
            "_FakeBouncePreventBlowout",
            "_FakeBounceStrength",
            "_FakeBounceBlur",
            "_FakeBounceBlendMode",
            "_FakeBounceInvertMask",
            "_FakeBounceMaskStrength",
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

        private static MethodInfo _syncMethod;
        private static MethodInfo _migrateMethod;
        private static bool _syncResolveAttempted;

        private static Dictionary<string, Shader> _nameToSSRT; // base lilToon シェーダー名 → lilSSRT シェーダー
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
        /// Variant はルート親を変換、参照不一致は名前 fallback。
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
            finally
            {
                // 公式変換は lilShaderManager の共有シェーダー参照を lilSSRT 版に差し替えたまま返る。
                // 残すと、以後の lilToonPreset.ApplyPreset が素の lilToon マテリアルまで lilSSRT にしてしまう。
                lilShaderManager.InitializeShaders();
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
        /// 対象を lilSSRT 化し、参照の AO / FakeBounce 設定を写して、シェーダーとキーワードを確定させる。
        /// </summary>
        public static bool ApplyReference(Material reference, Material target)
        {
            if (reference == null || target == null)
            {
                return false;
            }

            ConvertToLilSSRT(target);

            if (!IsLilSSRTMaterial(ResolveShaderOwner(target)))
            {
                return false;
            }

            // 旧キーワードの移行は AO の有効/アルゴリズムを書き換えるので、値を写す前に済ませる。
            MigrateLegacyKeywords(target);

            CopyAOProperties(reference, target);
            CopyFakeBounceProperties(reference, target);

            // シェーダー系統（RTAO / GTAO / AO なし）・AO テッセレーション版・キーワードは lilSSRT が
            // プロパティ値から導出する。値を写しただけでは古いシェーダーとキーワードのまま残る。
            SyncWithLilSSRT(target);

            return true;
        }

        /// <summary>
        /// 対象を lilSSRT 化し、対象自身に残っている AO 設定からシェーダーとキーワードを確定させる（参照からは何も写さない）。
        /// lilSSRT だったマテリアルが lilToon シェーダーへ戻された後、元の lilSSRT へ戻すのに使う。
        /// </summary>
        public static bool ConvertKeepingSettings(Material target)
        {
            if (target == null)
            {
                return false;
            }

            ConvertToLilSSRT(target);

            if (!IsLilSSRTMaterial(ResolveShaderOwner(target)))
            {
                return false;
            }

            MigrateLegacyKeywords(target);
            SyncWithLilSSRT(target);

            return true;
        }

        /// <summary>
        /// 検証済みの lilSSRT 参照マテリアルから AO のプロパティ値を対象へコピーする。
        /// マスクテクスチャ・デバッグ表示は触らない。シェーダーとキーワードは更新しない（ApplyReference を使う）。
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

            foreach (string prop in _aoColorProperties)
            {
                if (reference.HasProperty(prop) && target.HasProperty(prop))
                {
                    target.SetColor(prop, reference.GetColor(prop));
                }
            }
        }

        /// <summary>
        /// 参照から FakeBounce（疑似バウンスライト）の設定を対象へコピーする。
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

        private static void MigrateLegacyKeywords(Material material)
        {
            if (!EnsureSyncMethods() || _migrateMethod == null)
            {
                return;
            }

            try
            {
                _migrateMethod.Invoke(null, new object[] { material });
            }
            catch (TargetInvocationException e)
            {
                Debug.LogError(
                    $"[BulkMat] lilSSRT の旧キーワード移行に失敗しました ({material.name}): {e.InnerException?.Message ?? e.Message}"
                );
            }
        }

        // lilSSRT がインスペクタ表示時に行う同期（シェーダー系統・AO テッセレーション版・キーワードの確定）を呼ぶ。
        private static void SyncWithLilSSRT(Material material)
        {
            if (!EnsureSyncMethods())
            {
                return;
            }

            try
            {
                _syncMethod.Invoke(null, new object[] { material, true, true });
            }
            catch (TargetInvocationException e)
            {
                Debug.LogError(
                    $"[BulkMat] lilSSRT の同期に失敗しました ({material.name}): {e.InnerException?.Message ?? e.Message}"
                );
            }
        }

        private static bool EnsureSyncMethods()
        {
            if (_syncResolveAttempted)
            {
                return _syncMethod != null;
            }

            _syncResolveAttempted = true;

            const BindingFlags flags =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            _syncMethod = Type.GetType(VariantsTypeName)
                ?.GetMethod(
                    SyncMethodName,
                    flags,
                    null,
                    new[] { typeof(Material), typeof(bool), typeof(bool) },
                    null
                );
            _migrateMethod = Type.GetType(MigrationTypeName)
                ?.GetMethod(MigrateMethodName, flags, null, new[] { typeof(Material) }, null);
            if (_syncMethod == null)
            {
                Debug.LogWarning(
                    "[BulkMat] lilSSRT の同期メソッドを解決できません。シェーダーとキーワードは lilSSRT のインスペクタを開くまで更新されません。lilSSRT のバージョンを確認してください。"
                );
            }

            return _syncMethod != null;
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
                }

                _nameToSSRT = map;
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
