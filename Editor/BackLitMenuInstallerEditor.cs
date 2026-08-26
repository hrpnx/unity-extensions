using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Hrpnx.UnityExtensions.BackLitMenuInstaller
{
    /// <summary>
    /// BackLitMenuInstaller のインスペクタ。
    /// 逆光ライトのラベルと並びは lilToon 本体に合わせている
    /// (Packages/jp.lilxyzw.liltoon/Editor/Localization/ja-JP.po および lilMainInspectorGUI.cs)。
    /// </summary>
    [CustomEditor(typeof(BackLitMenuInstaller))]
    public class BackLitMenuInstallerEditor : Editor
    {
        private static readonly GUIContent ExcludedRenderersLabel = new(
            "除外する Renderer",
            "逆光ライトを適用しない Renderer"
        );
        private static readonly GUIContent MenuHeader = new("メニュー");
        private static readonly GUIContent DefaultLabel = new(
            "デフォルトでオン",
            "メニューの初期状態"
        );
        private static readonly GUIContent SavedLabel = new(
            "パラメータを保存",
            "ワールド移動やアバター再読み込みをまたいで状態を保持する"
        );
        private static readonly GUIContent RootMenuLabel = new(
            "追加先メニュー",
            "メニューを追加するルートメニュー"
        );

        private static readonly GUIContent BacklightHeader = new("逆光ライト");
        private static readonly GUIContent ColorLabel = new("色");
        private static readonly GUIContent MainStrengthLabel = new("メインカラーの強度");
        private static readonly GUIContent ReceiveShadowLabel = new("影を受け取る");
        private static readonly GUIContent BackfaceMaskLabel = new("裏面で無効化");
        private static readonly GUIContent NormalStrengthLabel = new("ノーマルマップ強度");
        private static readonly GUIContent BorderLabel = new(
            "範囲",
            "lilToon のインスペクタに表示される「範囲」と同じ値"
        );
        private static readonly GUIContent BlurLabel = new("ぼかし");
        private static readonly GUIContent DirectivityLabel = new("指向性");
        private static readonly GUIContent ViewStrengthLabel = new("視線方向の影響度");

        private SerializedProperty _excludedRenderers;
        private SerializedProperty _default;
        private SerializedProperty _saved;
        private SerializedProperty _rootMenu;
        private SerializedProperty _color;
        private SerializedProperty _mainStrength;
        private SerializedProperty _receiveShadow;
        private SerializedProperty _backfaceMask;
        private SerializedProperty _normalStrength;
        private SerializedProperty _border;
        private SerializedProperty _blur;
        private SerializedProperty _directivity;
        private SerializedProperty _viewStrength;

        private ReorderableList _excludedRenderersList;

        private void OnEnable()
        {
            _excludedRenderers = serializedObject.FindProperty(
                nameof(BackLitMenuInstaller.ExcludedRenderers)
            );
            _default = serializedObject.FindProperty(nameof(BackLitMenuInstaller.Default));
            _saved = serializedObject.FindProperty(nameof(BackLitMenuInstaller.Saved));
            _rootMenu = serializedObject.FindProperty(nameof(BackLitMenuInstaller.RootMenu));
            _color = serializedObject.FindProperty(nameof(BackLitMenuInstaller.Color));
            _mainStrength = serializedObject.FindProperty(
                nameof(BackLitMenuInstaller.MainStrength)
            );
            _receiveShadow = serializedObject.FindProperty(
                nameof(BackLitMenuInstaller.ReceiveShadow)
            );
            _backfaceMask = serializedObject.FindProperty(
                nameof(BackLitMenuInstaller.BackfaceMask)
            );
            _normalStrength = serializedObject.FindProperty(
                nameof(BackLitMenuInstaller.NormalStrength)
            );
            _border = serializedObject.FindProperty(nameof(BackLitMenuInstaller.Border));
            _blur = serializedObject.FindProperty(nameof(BackLitMenuInstaller.Blur));
            _directivity = serializedObject.FindProperty(
                nameof(BackLitMenuInstaller.Directivity)
            );
            _viewStrength = serializedObject.FindProperty(
                nameof(BackLitMenuInstaller.ViewStrength)
            );

            _excludedRenderersList = new ReorderableList(
                serializedObject,
                _excludedRenderers,
                true,
                true,
                true,
                true
            )
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, ExcludedRenderersLabel),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    rect.y += EditorGUIUtility.standardVerticalSpacing;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.PropertyField(
                        rect,
                        _excludedRenderers.GetArrayElementAtIndex(index),
                        GUIContent.none
                    );
                },
                onAddCallback = list =>
                {
                    int index = _excludedRenderers.arraySize;
                    _excludedRenderers.arraySize++;
                    // 既定では直前の要素の値がコピーされるため明示的に初期化する
                    _excludedRenderers.GetArrayElementAtIndex(index).objectReferenceValue = null;
                    list.index = index;
                },
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _excludedRenderersList.DoLayoutList();
            HandleDragAndDrop(GUILayoutUtility.GetLastRect());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(MenuHeader, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_default, DefaultLabel);
            EditorGUILayout.PropertyField(_saved, SavedLabel);
            EditorGUILayout.PropertyField(_rootMenu, RootMenuLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(BacklightHeader, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_color, ColorLabel);
            EditorGUILayout.PropertyField(_mainStrength, MainStrengthLabel);
            EditorGUILayout.PropertyField(_receiveShadow, ReceiveShadowLabel);
            EditorGUILayout.PropertyField(_backfaceMask, BackfaceMaskLabel);
            EditorGUILayout.PropertyField(_normalStrength, NormalStrengthLabel);
            EditorGUILayout.PropertyField(_border, BorderLabel);
            EditorGUILayout.PropertyField(_blur, BlurLabel);
            EditorGUILayout.PropertyField(_directivity, DirectivityLabel);
            EditorGUILayout.PropertyField(_viewStrength, ViewStrengthLabel);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// リストの矩形へ Renderer / GameObject をドロップして除外対象に追加する。
        /// </summary>
        private void HandleDragAndDrop(Rect rect)
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            {
                return;
            }

            if (!rect.Contains(evt.mousePosition))
            {
                return;
            }

            var renderers = CollectRenderers(DragAndDrop.objectReferences);
            if (renderers.Count == 0)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddRenderers(renderers);
            }

            evt.Use();
        }

        private static List<Renderer> CollectRenderers(Object[] draggedObjects)
        {
            var result = new List<Renderer>();
            foreach (var dragged in draggedObjects)
            {
                var gameObject = dragged as GameObject;
                if (gameObject == null)
                {
                    if (dragged is Renderer renderer)
                    {
                        result.Add(renderer);
                    }
                    continue;
                }

                // グループごとドロップして一括除外できるよう子も拾う
                result.AddRange(gameObject.GetComponentsInChildren<Renderer>(true));
            }

            return result;
        }

        private void AddRenderers(List<Renderer> renderers)
        {
            var existing = new HashSet<Object>();
            for (int i = 0; i < _excludedRenderers.arraySize; i++)
            {
                var value = _excludedRenderers.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value != null)
                {
                    existing.Add(value);
                }
            }

            foreach (var renderer in renderers)
            {
                if (renderer == null || !existing.Add(renderer))
                {
                    continue;
                }

                int index = _excludedRenderers.arraySize;
                _excludedRenderers.arraySize++;
                _excludedRenderers.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            }
        }
    }
}
