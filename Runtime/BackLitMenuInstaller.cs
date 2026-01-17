using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace Hrpnx.UnityExtensions.BackLitMenuInstaller
{
    /// <summary>
    /// ビルド時に lilToon の BackLit メニューを自動生成するコンポーネント
    /// </summary>
    public class BackLitMenuInstaller : MonoBehaviour, IEditorOnly
    {
        [Tooltip("BackLit の設定を適用しないマテリアル")]
        public List<Material> Exclusions = new();

        [Tooltip("メニューのデフォルト状態")]
        public bool Default;

        [Tooltip("パラメータを保存するかどうか")]
        public bool Saved;

        [Tooltip("BackLit の色 (HDR)")]
        [ColorUsage(true, true)]
        public Color Color = new(12, 12, 12, 1);

        [Tooltip("メインの強さ")]
        public float MainStrength = 0.5f;

        [Tooltip("法線の強さ")]
        public float NormalStrength = 1f;

        [Tooltip("境界")]
        public float Border = 0.6f;

        [Tooltip("ぼかし")]
        public float Blur = 0.2f;

        [Tooltip("指向性")]
        public float Directivity = 10f;

        [Tooltip("視点からの強さ")]
        public float ViewStrength = 1f;

        [Tooltip("影の受け取り")]
        public float ReceiveShadow = 1f;

        [Tooltip("メニューを追加するルートメニュー")]
        public VRCExpressionsMenu RootMenu;
    }
}
