using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace Hrpnx.UnityExtensions.BackLitMenuInstaller
{
    /// <summary>
    /// ビルド時に lilToon の逆光ライトメニューを自動生成するコンポーネント。
    /// 逆光ライトのフィールドは lilToon のインスペクタ (逆光ライト) と同じ並び・同じ意味にしている。
    /// </summary>
    public class BackLitMenuInstaller : MonoBehaviour, IEditorOnly
    {
        [Tooltip("逆光ライトを適用しない Renderer")]
        public List<Renderer> ExcludedRenderers = new();

        [Tooltip("メニューのデフォルト状態")]
        public bool Default;

        [Tooltip("パラメータを保存するかどうか")]
        public bool Saved;

        [Tooltip("メニューを追加するルートメニュー")]
        public VRCExpressionsMenu RootMenu;

        // ここから下は lilToon の「逆光ライト」セクションと同じ並び

        [ColorUsage(true, true)]
        public Color Color = new(12, 12, 12, 1);

        [Range(0f, 1f)]
        public float MainStrength = 0.5f;

        public bool ReceiveShadow = true;

        public bool BackfaceMask = true;

        [Range(0f, 1f)]
        public float NormalStrength = 1f;

        /// <summary>
        /// lilToon のインスペクタに表示される「範囲」と同じ値。
        /// シェーダーの _BacklightBorder は反転値 (1 - この値) を持つため、書き込み時に反転する。
        /// </summary>
        [Range(0f, 1f)]
        public float Border = 0.6f;

        [Range(0f, 1f)]
        public float Blur = 0.2f;

        public float Directivity = 10f;

        [Range(0f, 1f)]
        public float ViewStrength = 1f;
    }
}
