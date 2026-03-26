using System.Collections.Generic;
using lilToon;
using UnityEditor;
using UnityEngine;

namespace Hrpnx.UnityExtensions.BulkMat
{
    public class BulkMat : EditorWindow
    {
        private const string LILTOON_SHADER_NAME = "lilToon";
        private const string PREF_TARGET_DIRECTORY = "BulkMat_TargetDirectory";
        private const string PREF_PRESET = "BulkMat_Preset";
        private const string PREF_INCLUDE_SUBFOLDERS = "BulkMat_IncludeSubFolders";
        private const string PREF_OVERRIDE_OUTLINE = "BulkMat_OverrideOutline";
        private const string PREF_USE_OUTLINE = "BulkMat_UseOutline";

        private DefaultAsset _targetDirectory;
        private ScriptableObject _preset;
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
                    + "2. lilToonプリセットを設定\n"
                    + "3. 「マテリアルに一括適用」ボタンをクリック\n\n"
                    + "※ lilToon以外のシェーダーは自動的にスキップされます",
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

            GUI.enabled = _targetDirectory != null && _preset != null;
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

            if (_preset == null)
            {
                Debug.LogError("プリセットが指定されていません");
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
            int processed = ApplyPresetToMaterials(materials, _preset, outlineOverride);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"ディレクトリ一括適用完了: {directoryPath}\n"
                    + $"{processed}個のlilToonマテリアルにプリセットを適用しました (全{materials.Count}個中)"
            );
        }

        private int ApplyPresetToMaterials(
            List<Material> materials,
            ScriptableObject config,
            bool? outlineOverride
        )
        {
            bool isLilToonPreset = config is lilToonPreset;

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

                if (material.shader == null || !material.shader.name.Contains(LILTOON_SHADER_NAME))
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

            var targetShader = Shader.Find(targetName);
            if (targetShader == null)
            {
                Debug.LogWarning(
                    $"[BulkMat] シェーダー '{targetName}' が見つかりません。({material.name})"
                );
                return;
            }

            material.shader = targetShader;
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
