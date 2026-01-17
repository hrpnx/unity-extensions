using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Hrpnx.UnityExtensions.VRCFallbackSetter
{
    /// <summary>
    /// ビルド時にアバター配下の全マテリアルの VRChat Custom Safety Fallback を設定するコンポーネント
    /// </summary>
    public class VRCFallbackSetter : MonoBehaviour, IEditorOnly
    {
        [Tooltip("全マテリアルに適用する VRCFallback シェーダータイプ")]
        public VRCFallbackShaderType ShaderType = VRCFallbackShaderType.Hidden;

        [Tooltip("全マテリアルに適用する VRCFallback レンダリングモード")]
        public VRCFallbackRenderingMode RenderingMode = VRCFallbackRenderingMode.Opaque;

        [Tooltip("全マテリアルに適用する VRCFallback カリングモード")]
        public VRCFallbackFacing Facing = VRCFallbackFacing.Default;

        [Tooltip("VRCFallback の設定を適用しないマテリアル")]
        public List<Material> Exclusions = new();
    }
}
