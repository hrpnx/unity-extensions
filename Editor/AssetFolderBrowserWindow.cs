using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hrpnx.UnityExtensions.AssetFolderBrowser
{
    public class AssetFolderBrowserWindow : EditorWindow
    {
        private static readonly HashSet<string> ExcludedTopLevelFolders = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "Editor",
            "Plugins",
            "StreamingAssets",
            "Resources",
            "Gizmos",
        };

        private enum SortMode
        {
            ShopNameAsc,
            ShopNameDesc,
            AssetNameAsc,
            AssetNameDesc,
            SizeAsc,
            SizeDesc,
        }

        private record FolderEntry(
            string ShopName,
            string AssetName,
            string AssetPath,
            long SizeBytes
        )
        {
            public bool IsSelected { get; set; }
            public bool? HasSceneRef { get; set; }
        }

        private List<FolderEntry> _allEntries = new List<FolderEntry>();
        private List<FolderEntry> _filteredEntries = new List<FolderEntry>();
        private SortMode _sortMode = SortMode.SizeDesc;
        private Vector2 _scrollPos;
        private bool _sceneRefChecked;
        private string _searchFilter = string.Empty;

        [MenuItem("Tools/Asset Folder Browser")]
        public static void ShowWindow()
        {
            var window = GetWindow<AssetFolderBrowserWindow>("Asset Folder Browser");
            window.minSize = new Vector2(660, 500);
            window.Show();
        }

        private void OnEnable() => Scan();

        private void Scan()
        {
            _allEntries = new List<FolderEntry>();
            _sceneRefChecked = false;

            try
            {
                foreach (string shopDir in Directory.GetDirectories(Application.dataPath))
                {
                    string shopName = Path.GetFileName(shopDir);
                    if (ExcludedTopLevelFolders.Contains(shopName))
                        continue;

                    foreach (string assetDir in Directory.GetDirectories(shopDir))
                    {
                        string assetName = Path.GetFileName(assetDir);
                        string assetPath = $"Assets/{shopName}/{assetName}";
                        long size = CalculateFolderSize(assetDir);
                        _allEntries.Add(new FolderEntry(shopName, assetName, assetPath, size));
                    }
                }
            }
            catch (IOException ex)
            {
                Debug.LogError($"[AssetFolderBrowser] スキャン失敗: {ex.Message}");
            }

            ApplyFilterAndSort();
        }

        private static long CalculateFolderSize(string path)
        {
            try
            {
                return Directory
                    .GetFiles(path, "*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    .Sum(f => new FileInfo(f).Length);
            }
            catch (IOException)
            {
                return 0;
            }
        }

        private void CheckSceneReferences()
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });

            if (sceneGuids.Length == 0)
            {
                foreach (var entry in _allEntries)
                    entry.HasSceneRef = false;
                _sceneRefChecked = true;
                Repaint();
                return;
            }

            string[] scenePaths = sceneGuids.Select(AssetDatabase.GUIDToAssetPath).ToArray();

            var deps = new HashSet<string>(
                AssetDatabase.GetDependencies(scenePaths, recursive: true),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var entry in _allEntries)
            {
                bool hasRef = AssetDatabase
                    .FindAssets(string.Empty, new[] { entry.AssetPath })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Any(deps.Contains);
                entry.HasSceneRef = hasRef;
            }

            _sceneRefChecked = true;
            ApplyFilterAndSort();
            Repaint();
        }

        private void ApplyFilterAndSort()
        {
            IEnumerable<FolderEntry> result = _allEntries;

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                result = result.Where(e =>
                    e.ShopName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
                    || e.AssetName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
                );
            }

            result = _sortMode switch
            {
                SortMode.ShopNameAsc => result
                    .OrderBy(e => e.ShopName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.AssetName, StringComparer.OrdinalIgnoreCase),
                SortMode.ShopNameDesc => result
                    .OrderByDescending(e => e.ShopName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.AssetName, StringComparer.OrdinalIgnoreCase),
                SortMode.AssetNameAsc => result.OrderBy(
                    e => e.AssetName,
                    StringComparer.OrdinalIgnoreCase
                ),
                SortMode.AssetNameDesc => result.OrderByDescending(
                    e => e.AssetName,
                    StringComparer.OrdinalIgnoreCase
                ),
                SortMode.SizeAsc => result.OrderBy(e => e.SizeBytes),
                SortMode.SizeDesc => result.OrderByDescending(e => e.SizeBytes),
                _ => result,
            };

            _filteredEntries = result.ToList();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawHeader();
            DrawList();
            DrawFooter();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("再スキャン", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Scan();

                GUILayout.Space(4);

                string sceneRefLabel = _sceneRefChecked
                    ? "シーン参照: チェック済"
                    : "シーン参照を確認";
                if (
                    GUILayout.Button(
                        sceneRefLabel,
                        EditorStyles.toolbarButton,
                        GUILayout.Width(150)
                    )
                )
                    CheckSceneReferences();

                GUILayout.FlexibleSpace();

                GUILayout.Label("検索:", EditorStyles.miniLabel, GUILayout.Width(35));
                string newFilter = GUILayout.TextField(
                    _searchFilter,
                    EditorStyles.toolbarSearchField,
                    GUILayout.Width(200)
                );
                if (newFilter != _searchFilter)
                {
                    _searchFilter = newFilter;
                    ApplyFilterAndSort();
                }
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
            {
                bool allSelected =
                    _filteredEntries.Count > 0 && _filteredEntries.All(e => e.IsSelected);
                bool newAllSelected = EditorGUILayout.Toggle(allSelected, GUILayout.Width(20));
                if (newAllSelected != allSelected)
                {
                    foreach (var entry in _filteredEntries)
                        entry.IsSelected = newAllSelected;
                }

                DrawSortColumnHeader(
                    "ショップ名",
                    SortMode.ShopNameAsc,
                    SortMode.ShopNameDesc,
                    140
                );
                DrawSortColumnHeader(
                    "アセット名",
                    SortMode.AssetNameAsc,
                    SortMode.AssetNameDesc,
                    190
                );
                DrawSortColumnHeader("サイズ", SortMode.SizeAsc, SortMode.SizeDesc, 90);
                GUILayout.Label("シーン参照", EditorStyles.boldLabel, GUILayout.Width(70));
                GUILayout.Label(string.Empty, GUILayout.Width(50));
            }
        }

        private void DrawSortColumnHeader(string label, SortMode asc, SortMode desc, float width)
        {
            string indicator =
                _sortMode == asc ? " ▲"
                : _sortMode == desc ? " ▼"
                : string.Empty;
            if (GUILayout.Button(label + indicator, EditorStyles.boldLabel, GUILayout.Width(width)))
            {
                _sortMode = _sortMode == asc ? desc : asc;
                ApplyFilterAndSort();
            }
        }

        private void DrawList()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var entry in _filteredEntries)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.IsSelected = EditorGUILayout.Toggle(
                        entry.IsSelected,
                        GUILayout.Width(20)
                    );
                    GUILayout.Label(entry.ShopName, GUILayout.Width(140));
                    GUILayout.Label(entry.AssetName, GUILayout.Width(190));
                    GUILayout.Label(FormatSize(entry.SizeBytes), GUILayout.Width(90));

                    if (entry.HasSceneRef is null)
                    {
                        GUILayout.Label("-", GUILayout.Width(70));
                    }
                    else
                    {
                        var prevColor = GUI.contentColor;
                        GUI.contentColor = entry.HasSceneRef.Value
                            ? new Color(0.4f, 0.9f, 0.4f)
                            : new Color(0.9f, 0.5f, 0.5f);
                        GUILayout.Label(
                            entry.HasSceneRef.Value ? "あり" : "なし",
                            GUILayout.Width(70)
                        );
                        GUI.contentColor = prevColor;
                    }

                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                            entry.AssetPath
                        );
                        if (asset != null)
                            EditorGUIUtility.PingObject(asset);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawFooter()
        {
            var selected = _filteredEntries.Where(e => e.IsSelected).ToList();
            long totalSelected = selected.Sum(e => e.SizeBytes);
            long totalAll = _allEntries.Sum(e => e.SizeBytes);

            using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label(
                    $"表示: {_filteredEntries.Count} / 全 {_allEntries.Count} フォルダ  |  合計: {FormatSize(totalAll)}"
                );
                GUILayout.FlexibleSpace();
                GUILayout.Label($"選択: {selected.Count} フォルダ ({FormatSize(totalSelected)})");
                GUILayout.Space(8);

                GUI.enabled = selected.Count > 0;
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = selected.Count > 0 ? new Color(1f, 0.45f, 0.45f) : Color.gray;
                if (
                    GUILayout.Button(
                        $"選択した {selected.Count} フォルダを削除",
                        GUILayout.Width(210)
                    )
                )
                    DeleteSelected(selected);
                GUI.backgroundColor = prevBg;
                GUI.enabled = true;
            }
        }

        private void DeleteSelected(List<FolderEntry> selected)
        {
            string names = string.Join("\n", selected.Select(e => $"  {e.AssetPath}"));
            long totalSize = selected.Sum(e => e.SizeBytes);

            bool confirmed = EditorUtility.DisplayDialog(
                "フォルダ削除の確認",
                $"{selected.Count} フォルダを削除します ({FormatSize(totalSize)}):\n\n{names}\n\nこの操作は元に戻せません。",
                "削除する",
                "キャンセル"
            );

            if (!confirmed)
                return;

            int deleted = 0;
            foreach (var entry in selected)
            {
                if (AssetDatabase.DeleteAsset(entry.AssetPath))
                    deleted++;
                else
                    Debug.LogWarning($"[AssetFolderBrowser] 削除失敗: {entry.AssetPath}");
            }

            Debug.Log($"[AssetFolderBrowser] {deleted}/{selected.Count} フォルダを削除しました");
            AssetDatabase.Refresh();
            Scan();
        }

        private static string FormatSize(long bytes) =>
            bytes switch
            {
                >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
                >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
                >= 1_024 => $"{bytes / 1_024.0:F1} KB",
                _ => $"{bytes} B",
            };
    }
}
