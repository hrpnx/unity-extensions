using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Hrpnx.UnityExtensions.VRCFallbackSetter
{
    /// <summary>
    /// ビルド時にアバター配下の全マテリアルの VRChat Custom Safety Fallback の Shader Type を設定するコンポーネント
    /// </summary>
    public class VRCFallbackSetter : MonoBehaviour, IEditorOnly
    {
        [Tooltip("全マテリアルに適用する VRCFallback シェーダータイプ")]
        public VRCFallbackType FallbackType = VRCFallbackType.Hidden;

        [Tooltip("VRCFallback の設定を適用しないマテリアル")]
        public List<Material> Exclusions = new List<Material>();
    }
}
