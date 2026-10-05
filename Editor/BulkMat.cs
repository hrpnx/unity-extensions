using System.Collections.Generic;
using System.Linq;
using lilToon;
using UnityEditor;
using UnityEngine;

namespace Hrpnx.UnityExtensions.BulkMat
{
    public class BulkMat : EditorWindow
    {
        private const string LILTOON_SHADER_NAME = "lilToon";
        private const string LILSSRT_SHADER_NAME = "lilSSRT";
        private const string PREF_TARGET_DIRECTORY = "BulkMat_TargetDirectory";
        private const string PREF_PRESET = "BulkMat_Preset";
        private const string PREF_SSRTREF = "BulkMat_SSRTRef";
        private const string PREF_INCLUDE_SUBFOLDERS = "BulkMat_IncludeSubFolders";
        private const string PREF_OVERRIDE_OUTLINE = "BulkMat_OverrideOutline";
        private const string PREF_USE_OUTLINE = "BulkMat_UseOutline";

        private DefaultAsset _targetDirectory;
        private ScriptableObject _preset;
        private Material _ssrtRef;
        private bool _includeSubFolders = true;
        private bool _overrideOutline = false;
        private bool _useOutline = true;

        [MenuItem(MenuPaths.Root + "BulkMat")]
        public static void ShowWindow()
        {
            var window = GetWindow<BulkMat>("BulkMat");
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void OnDisable()
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            string directoryPath = EditorPrefs.GetString(PREF_TARGET_DIRECTORY, "");
            if (!string.IsNullOrEmpty(directoryPath))
            {
                _targetDirectory = AssetDatabase.LoadAssetAtPath<DefaultAsset>(directoryPath);
            }

            string presetPath = EditorPrefs.GetString(PREF_PRESET, "");
            if (!string.IsNullOrEmpty(presetPath))
            {
                _preset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(presetPath);
            }

            string ssrtRefPath = EditorPrefs.GetString(PREF_SSRTREF, "");
            if (!string.IsNullOrEmpty(ssrtRefPath))
            {
                _ssrtRef = AssetDatabase.LoadAssetAtPath<Material>(ssrtRefPath);
            }

            _includeSubFolders = EditorPrefs.GetBool(PREF_INCLUDE_SUBFOLDERS, true);
            _overrideOutline = EditorPrefs.GetBool(PREF_OVERRIDE_OUTLINE, false);
            _useOutline = EditorPrefs.GetBool(PREF_USE_OUTLINE, true);
        }

        private void SaveSettings()
        {
            string directoryPath =
                _targetDirectory != null ? AssetDatabase.GetAssetPath(_targetDirectory) : "";
            EditorPrefs.SetString(PREF_TARGET_DIRECTORY, directoryPath);

            string presetPath = _preset != null ? AssetDatabase.GetAssetPath(_preset) : "";
            EditorPrefs.SetString(PREF_PRESET, presetPath);

            string ssrtRefPath = _ssrtRef != null ? AssetDatabase.GetAssetPath(_ssrtRef) : "";
            EditorPrefs.SetString(PREF_SSRTREF, ssrtRefPath);

            EditorPrefs.SetBool(PREF_INCLUDE_SUBFOLDERS, _includeSubFolders);
            EditorPrefs.SetBool(PREF_OVERRIDE_OUTLINE, _overrideOutline);
            EditorPrefs.SetBool(PREF_USE_OUTLINE, _useOutline);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("lilToon プリセット一括適用ツール", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "使い方:\n"
                    + "1. 対象フォルダにマテリアルが含まれるフォルダを設定\n"
                    + "2. lilToonプリセットを設定（任意）\n"
                    + "3. lilSSRT参照マテリアルを設定（任意・lilSSRT化 + AO/FakeBounceコピー）\n"
                    + "4. 「マテリアルに一括適用」ボタンをクリック\n\n"
                    + "※ lilToon / lilSSRT 以外のシェーダーは自動的にスキップされます",
                MessageType.Info
            );

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("対象フォルダ:", GUILayout.Width(100));
            _targetDirectory = (DefaultAsset)
                EditorGUILayout.ObjectField(_targetDirectory, typeof(DefaultAsset), false);
            EditorGUILayout.EndHorizontal();

            if (_targetDirectory != null)
            {
                string path = AssetDatabase.GetAssetPath(_targetDirectory);
                EditorGUILayout.LabelField($"パス: {path}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("プリセット:", GUILayout.Width(100));
            _preset = (ScriptableObject)
                EditorGUILayout.ObjectField(_preset, typeof(ScriptableObject), false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("lilSSRT参照:", GUILayout.Width(100));
            _ssrtRef = (Material)EditorGUILayout.ObjectField(_ssrtRef, typeof(Material), false);
            EditorGUILayout.EndHorizontal();

            _includeSubFolders = EditorGUILayout.Toggle("サブフォルダを含める", _includeSubFolders);

            EditorGUILayout.Space();

            _overrideOutline = EditorGUILayout.Toggle("輪郭線を上書き", _overrideOutline);
            GUI.enabled = _overrideOutline;
            EditorGUI.indentLevel++;
            _useOutline = EditorGUILayout.Toggle("輪郭線を有効にする", _useOutline);
            EditorGUI.indentLevel--;
            GUI.enabled = true;

            EditorGUILayout.Space();

            if (_targetDirectory != null)
            {
                string directoryPath = AssetDatabase.GetAssetPath(_targetDirectory);
                int materialCount = CountMaterialsInDirectory(directoryPath, _includeSubFolders);
                EditorGUILayout.LabelField(
                    $"検出マテリアル数: {materialCount}",
                    EditorStyles.miniLabel
                );
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            GUI.enabled = _targetDirectory != null && (_preset != null || _ssrtRef != null);
            if (GUILayout.Button("マテリアルに一括適用", GUILayout.Height(30)))
            {
                ApplyToDirectory();
            }
            GUI.enabled = true;
        }

        private int CountMaterialsInDirectory(string directoryPath, bool includeSubFolders)
        {
            if (includeSubFolders)
            {
                string[] guids = AssetDatabase.FindAssets("t:Material", new[] { directoryPath });
                return guids.Length;
            }
            else
            {
                string[] files = System.IO.Directory.GetFiles(directoryPath, "*.mat");
                return files.Length;
            }
        }

        private void ApplyToDirectory()
        {
            if (_targetDirectory == null)
            {
                Debug.LogError("対象フォルダが指定されていません");
                return;
            }

            if (_preset == null && _ssrtRef == null)
            {
                Debug.LogError("プリセットも lilSSRT 参照も指定されていません");
                return;
            }

            string directoryPath = AssetDatabase.GetAssetPath(_targetDirectory);
            if (string.IsNullOrEmpty(directoryPath))
            {
                Debug.LogError("フォルダパスの取得に失敗しました");
                return;
            }

            List<Material> materials;
            if (_includeSubFolders)
            {
                string[] materialGuids = AssetDatabase.FindAssets(
                    "t:Material",
                    new[] { directoryPath }
                );
                if (materialGuids.Length == 0)
                {
                    Debug.LogWarning(
                        $"指定されたフォルダ内にマテリアルが見つかりませんでした: {directoryPath}"
                    );
                    return;
                }

                materials = new List<Material>();
                foreach (string guid in materialGuids)
                {
                    string materialPath = AssetDatabase.GUIDToAssetPath(guid);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material != null)
                    {
                        materials.Add(material);
                    }
                }
            }
            else
            {
                string[] files = System.IO.Directory.GetFiles(directoryPath, "*.mat");
                if (files.Length == 0)
                {
                    Debug.LogWarning(
                        $"指定されたフォルダ内にマテリアルが見つかりませんでした: {directoryPath}"
                    );
                    return;
                }

                materials = new List<Material>();
                foreach (string file in files)
                {
                    string relativePath = file.Replace("\\", "/");
                    var material = AssetDatabase.LoadAssetAtPath<Material>(relativePath);
                    if (material != null)
                    {
                        materials.Add(material);
                    }
                }
            }

            bool? outlineOverride = _overrideOutline ? _useOutline : (bool?)null;

            ApplySettings(
                materials,
                _preset,
                _ssrtRef,
                outlineOverride,
                out int baseProcessed,
                out int ssrtProcessed
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"ディレクトリ一括適用完了: {directoryPath}\n"
                    + $"base {baseProcessed}個 / SSRT {ssrtProcessed}個 (全{materials.Count}個中)"
            );
        }

        // 順序: base preset → 輪郭線上書き → lilSSRT 変換 + AO/FakeBounce コピー。
        // outline 切替は lilToon シェーダー段階（SSRT 変換前）で確定させ、変換後もバリアントごと維持される。
        // AO プロパティは lilSSRT シェーダー上にしか存在しない。
        private static void ApplySettings(
            List<Material> materials,
            ScriptableObject preset,
            Material ssrtRef,
            bool? outlineOverride,
            out int baseProcessed,
            out int ssrtProcessed
        )
        {
            bool hasSSRT = ssrtRef != null;
            // SSRT 参照の検証（fail-fast）。不正なら SSRT ステップのみスキップし base 適用は継続。
            if (hasSSRT && !LilSSRTConverter.IsLilSSRTMaterial(ssrtRef))
            {
                Debug.LogError(
                    $"lilSSRT 参照が lilSSRT マテリアルではありません: {ssrtRef.name}。SSRT ステップをスキップします。"
                );
                hasSSRT = false;
            }

            // lilToonPreset の適用は lilSSRT のマテリアルも素の lilToon シェーダーへ戻すので、適用前に控えておく。
            var lilSSRTMaterials = materials.Where(LilSSRTConverter.IsLilSSRTMaterial).ToList();

            if (preset != null)
            {
                baseProcessed = ApplyPresetToMaterials(materials, preset, outlineOverride);
            }
            else if (outlineOverride.HasValue)
            {
                // プリセット未指定（lilSSRT 参照のみ）でも輪郭線上書きだけは適用する。
                baseProcessed = ApplyOutlineOverrideToMaterials(materials, outlineOverride.Value);
            }
            else
            {
                baseProcessed = 0;
            }

            ssrtProcessed = hasSSRT ? ApplySSRTToMaterials(materials, ssrtRef) : 0;
            if (!hasSSRT)
            {
                RestoreLilSSRT(lilSSRTMaterials);
            }
        }

        // lilSSRT 参照で変換し直さない場合に、適用前に lilSSRT だったマテリアルを lilSSRT へ戻す
        // （AO 設定はマテリアルに残っている値を使う）。
        private static void RestoreLilSSRT(List<Material> lilSSRTMaterials)
        {
            // Material Variant は親のシェーダーを継承するので、非 Variant を先に確定させる。
            foreach (var material in lilSSRTMaterials.OrderBy(m => m.isVariant))
            {
                if (LilSSRTConverter.IsLilSSRTMaterial(material))
                {
                    continue;
                }

                Undo.RecordObject(material, "lilSSRT化を維持");
                LilSSRTConverter.ConvertKeepingSettings(material);
                EditorUtility.SetDirty(material);
            }
        }

        // プリセットを通さず輪郭線の有効/無効だけを一括で上書きする。
        // SSRT 変換前（lilToon シェーダー段階）に呼ぶ前提なので ApplyOutlineShaderOnly が正しく名前解決できる。
        private static int ApplyOutlineOverrideToMaterials(List<Material> materials, bool enable)
        {
            int processedCount = 0;

            foreach (var material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (
                    material.shader == null
                    || (
                        !material.shader.name.Contains(LILTOON_SHADER_NAME)
                        && !material.shader.name.Contains(LILSSRT_SHADER_NAME)
                    )
                )
                {
                    continue;
                }

                Undo.RecordObject(material, "輪郭線設定を適用");
                ApplyOutlineShaderOnly(material, enable);
                EditorUtility.SetDirty(material);
                processedCount++;
            }

            return processedCount;
        }

        private static int ApplySSRTToMaterials(List<Material> materials, Material ssrtRef)
        {
            int processedCount = 0;

            // Material Variant は親のシェーダーを継承する。親より先に処理すると、lilSSRT の同期が
            // Variant 自身へシェーダーを設定しようとして Unity がエラーを出すので、非 Variant を先に確定させる。
            foreach (var material in materials.OrderBy(m => m != null && m.isVariant))
            {
                if (material == null)
                {
                    continue;
                }

                // lilToon / lilSSRT 以外は対象外。lilSSRT の Hidden バリアント（Hidden/lilSSRT/Fur 等）は
                // シェーダー名に "lilToon" を含まないため、"lilSSRT" も対象に含める
                // （既に lilSSRT 化されたマテリアルにも AO プロパティ再適用を効かせるため）。
                if (
                    material.shader == null
                    || (
                        !material.shader.name.Contains(LILTOON_SHADER_NAME)
                        && !material.shader.name.Contains(LILSSRT_SHADER_NAME)
                    )
                )
                {
                    continue;
                }

                Undo.RecordObject(material, "lilSSRT化を適用");

                LilSSRTConverter.ApplyReference(ssrtRef, material);

                EditorUtility.SetDirty(material);
                processedCount++;
            }

            return processedCount;
        }

        private static int ApplyPresetToMaterials(
            List<Material> materials,
            ScriptableObject config,
            bool? outlineOverride
        )
        {
            bool isLilToonPreset = config is lilToonPreset;

            // lilToonPreset.ApplyPreset は lilShaderManager の共有シェーダー参照から割り当て先を選ぶ。
            // 参照が lilSSRT 版に差し替わったままだと素の lilToon マテリアルまで lilSSRT になるので、必ず素の lilToon に戻す。
            if (isLilToonPreset)
            {
                lilShaderManager.InitializeShaders();
            }

            var serializedConfig = new SerializedObject(config);
            var colorsProperty = serializedConfig.FindProperty("colors");
            var floatsProperty = serializedConfig.FindProperty("floats");
            var vectorsProperty = serializedConfig.FindProperty("vectors");

            int processedCount = 0;

            foreach (var material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                // lilSSRT の Hidden バリアント（Hidden/lilSSRT/OpaqueOutline 等）はシェーダー名に
                // "lilToon" を含まない（"lilSSRT/lilToon" のみ含む）。lilSSRT も対象に含めないと
                // outline 等の lilSSRT マテリアルへ preset（影など）が適用されず取りこぼす。
                if (
                    material.shader == null
                    || (
                        !material.shader.name.Contains(LILTOON_SHADER_NAME)
                        && !material.shader.name.Contains(LILSSRT_SHADER_NAME)
                    )
                )
                {
                    continue;
                }

                Undo.RecordObject(material, "lilToonプリセットを適用");

                if (isLilToonPreset)
                {
                    var preset = (lilToonPreset)config;
                    lilToonPreset.ApplyPreset(material, preset, false);
                    ApplyOutline(material, preset, outlineOverride);
                }
                else
                {
                    ApplyPropertiesManually(
                        material,
                        colorsProperty,
                        floatsProperty,
                        vectorsProperty
                    );
                    if (outlineOverride.HasValue)
                    {
                        ApplyOutlineShaderOnly(material, outlineOverride.Value);
                    }
                }

                EditorUtility.SetDirty(material);
                processedCount++;
            }

            return processedCount;
        }

        // outline: -1（現状維持）のとき floats の _UseOutline を尊重、
        // outlineOverride が指定されていればそちらを優先する。
        private static void ApplyOutline(
            Material material,
            lilToonPreset preset,
            bool? outlineOverride
        )
        {
            bool enable;
            if (outlineOverride.HasValue)
            {
                enable = outlineOverride.Value;
            }
            else if (preset.outline == -1)
            {
                var entry = System.Array.Find(preset.floats, f => f.name == "_UseOutline");
                if (entry.name != "_UseOutline")
                {
                    return;
                }

                enable = entry.value >= 0.5f;
            }
            else
            {
                // preset.outline が 0/1 で明示されていれば ApplyPreset 側で処理済み
                return;
            }

            bool isCurrentlyOutline = lilShaderUtils.IsOutlineShaderName(material.shader.name);
            if (enable == isCurrentlyOutline)
            {
                return;
            }

            ApplyOutlineShaderOnly(material, enable);
        }

        private static void ApplyOutlineShaderOnly(Material material, bool enable)
        {
            bool isCurrentlyOutline = lilShaderUtils.IsOutlineShaderName(material.shader.name);
            if (enable == isCurrentlyOutline)
            {
                return;
            }

            string shaderName = material.shader.name;
            string targetName;
            if (enable)
            {
                targetName =
                    shaderName == "lilToon" ? "Hidden/lilToonOutline" : shaderName + "Outline";
            }
            else
            {
                targetName =
                    shaderName == "Hidden/lilToonOutline"
                        ? "lilToon"
                        : shaderName.Substring(0, shaderName.Length - "Outline".Length);
            }

            var targetShader = FindLilToonShader(targetName);
            if (targetShader == null)
            {
                Debug.LogWarning(
                    $"[BulkMat] シェーダー '{targetName}' が見つかりません。({material.name})"
                );
                return;
            }

            material.shader = targetShader;
        }

        // 公式 lilToon パッケージ配下のシェーダーを最優先で名前解決する。
        // 焼き込み/最適化シェーダーが lilToon の内部名（例: "Hidden/lilToonOutline"）を名乗ると、
        // Shader.Find は同名のうち1つ（プロジェクト内の焼き込み版など）を返すため、RimShade 等の機能を
        // 欠いたシェーダーが誤って割り当たる。パスに公式パッケージ名を含むものを優先して衝突を回避する。
        private const string LILTOON_PACKAGE_HINT = "jp.lilxyzw.liltoon";
        private static readonly Dictionary<string, Shader> _resolvedShaderCache =
            new Dictionary<string, Shader>();

        private static Shader FindLilToonShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
            {
                return null;
            }

            if (_resolvedShaderCache.TryGetValue(shaderName, out var cached) && cached != null)
            {
                return cached;
            }

            Shader official = null;
            Shader fallback = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Shader"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null || shader.name != shaderName)
                {
                    continue;
                }

                if (path.Contains(LILTOON_PACKAGE_HINT))
                {
                    official = shader;
                    break;
                }

                fallback = shader;
            }

            var resolved =
                official != null
                    ? official
                    : (fallback != null ? fallback : Shader.Find(shaderName));
            if (resolved != null)
            {
                _resolvedShaderCache[shaderName] = resolved;
            }

            return resolved;
        }

        private static void ApplyPropertiesManually(
            Material material,
            SerializedProperty colorsProperty,
            SerializedProperty floatsProperty,
            SerializedProperty vectorsProperty
        )
        {
            if (colorsProperty != null)
            {
                for (int i = 0; i < colorsProperty.arraySize; i++)
                {
                    var colorElement = colorsProperty.GetArrayElementAtIndex(i);
                    string colorName = colorElement.FindPropertyRelative("name").stringValue;
                    Color colorValue = colorElement.FindPropertyRelative("value").colorValue;

                    if (material.HasProperty(colorName))
                    {
                        material.SetColor(colorName, colorValue);
                    }
                }
            }

            if (floatsProperty != null)
            {
                for (int i = 0; i < floatsProperty.arraySize; i++)
                {
                    var floatElement = floatsProperty.GetArrayElementAtIndex(i);
                    string floatName = floatElement.FindPropertyRelative("name").stringValue;
                    float floatValue = floatElement.FindPropertyRelative("value").floatValue;

                    if (material.HasProperty(floatName))
                    {
                        material.SetFloat(floatName, floatValue);
                    }
                }
            }

            if (vectorsProperty != null)
            {
                for (int i = 0; i < vectorsProperty.arraySize; i++)
                {
                    var vectorElement = vectorsProperty.GetArrayElementAtIndex(i);
                    string vectorName = vectorElement.FindPropertyRelative("name").stringValue;
                    Vector4 vectorValue = vectorElement.FindPropertyRelative("value").vector4Value;

                    if (material.HasProperty(vectorName))
                    {
                        material.SetVector(vectorName, vectorValue);
                    }
                }
            }
        }
    }
}
