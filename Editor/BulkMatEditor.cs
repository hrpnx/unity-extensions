using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hrpnx.UnityExtensions.BulkMat
{
    public class BulkMatEditor : EditorWindow
    {
        private const string LILTOON_SHADER_NAME = "lilToon";
        private const string PREF_TARGET_DIRECTORY = "BulkMat_TargetDirectory";
        private const string PREF_CONFIG = "BulkMat_Config";
        private const string PREF_INCLUDE_SUBFOLDERS = "BulkMat_IncludeSubfolders";

        private DefaultAsset _targetDirectory;
        private ScriptableObject _config;
        private bool _includeSubfolders = true;

        [MenuItem("Tools/BulkMat")]
        public static void ShowWindow()
        {
            var window = GetWindow<BulkMatEditor>("BulkMat");
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

            string configPath = EditorPrefs.GetString(PREF_CONFIG, "");
            if (!string.IsNullOrEmpty(configPath))
            {
                _config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(configPath);
            }

            _includeSubfolders = EditorPrefs.GetBool(PREF_INCLUDE_SUBFOLDERS, true);
        }

        private void SaveSettings()
        {
            string directoryPath = _targetDirectory != null ? AssetDatabase.GetAssetPath(_targetDirectory) : "";
            EditorPrefs.SetString(PREF_TARGET_DIRECTORY, directoryPath);

            string configPath = _config != null ? AssetDatabase.GetAssetPath(_config) : "";
            EditorPrefs.SetString(PREF_CONFIG, configPath);

            EditorPrefs.SetBool(PREF_INCLUDE_SUBFOLDERS, _includeSubfolders);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ディレクトリ一括適用ツール", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "使い方:\n" +
                "1. 対象フォルダにマテリアルが含まれるフォルダを設定\n" +
                "2. 適用設定にMaterialPropertyConfigを設定\n" +
                "3. 「マテリアルに一括適用」ボタンをクリック\n\n" +
                "※ lilToon以外のシェーダーは自動的にスキップされます",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("対象フォルダ:", GUILayout.Width(100));
            _targetDirectory = (DefaultAsset)EditorGUILayout.ObjectField(_targetDirectory, typeof(DefaultAsset), false);
            EditorGUILayout.EndHorizontal();

            if (_targetDirectory != null)
            {
                string path = AssetDatabase.GetAssetPath(_targetDirectory);
                EditorGUILayout.LabelField($"パス: {path}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("適用設定:", GUILayout.Width(100));
            _config = (ScriptableObject)EditorGUILayout.ObjectField(_config, typeof(ScriptableObject), false);
            EditorGUILayout.EndHorizontal();

            _includeSubfolders = EditorGUILayout.Toggle("サブフォルダを含める", _includeSubfolders);

            EditorGUILayout.Space();

            if (_targetDirectory != null)
            {
                string directoryPath = AssetDatabase.GetAssetPath(_targetDirectory);
                int materialCount = CountMaterialsInDirectory(directoryPath, _includeSubfolders);
                EditorGUILayout.LabelField($"検出マテリアル数: {materialCount}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            GUI.enabled = _targetDirectory != null && _config != null;
            if (GUILayout.Button("マテリアルに一括適用", GUILayout.Height(30)))
            {
                ApplyToDirectory();
            }
            GUI.enabled = true;
        }

        private int CountMaterialsInDirectory(string directoryPath, bool includeSubfolders)
        {
            if (includeSubfolders)
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

            if (_config == null)
            {
                Debug.LogError("適用設定が指定されていません");
                return;
            }

            string directoryPath = AssetDatabase.GetAssetPath(_targetDirectory);
            if (string.IsNullOrEmpty(directoryPath))
            {
                Debug.LogError("フォルダパスの取得に失敗しました");
                return;
            }

            List<Material> materials;
            if (_includeSubfolders)
            {
                string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { directoryPath });
                if (materialGuids.Length == 0)
                {
                    Debug.LogWarning($"指定されたフォルダ内にマテリアルが見つかりませんでした: {directoryPath}");
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
                    Debug.LogWarning($"指定されたフォルダ内にマテリアルが見つかりませんでした: {directoryPath}");
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

            int processed = ApplyConfigToMaterials(materials, _config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"ディレクトリ一括適用完了: {directoryPath}\n" +
                      $"{processed}個のlilToonマテリアルに設定を適用しました (全{materials.Count}個中)");
        }

        private int ApplyConfigToMaterials(List<Material> materials, ScriptableObject config)
        {
            var serializedConfig = new SerializedObject(config);

            var colorsProperty = serializedConfig.FindProperty("colors");
            var floatsProperty = serializedConfig.FindProperty("floats");
            var vectorsProperty = serializedConfig.FindProperty("vectors");

            int processedCount = 0;

            foreach (var material in materials)
            {
                if (material == null) continue;

                if (material.shader == null || !material.shader.name.Contains(LILTOON_SHADER_NAME))
                {
                    continue;
                }

                Undo.RecordObject(material, "マテリアル設定を適用");

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

                EditorUtility.SetDirty(material);
                processedCount++;
            }

            return processedCount;
        }
    }
}
