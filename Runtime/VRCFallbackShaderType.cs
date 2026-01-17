namespace Hrpnx.UnityExtensions.VRCFallbackSetter
{
    /// <summary>
    /// VRChat Custom Safety Fallback のシェーダータイプ
    /// </summary>
    public enum VRCFallbackShaderType
    {
        Unlit,
        Standard,
        VertexLit,
        Toon,
        Particle,
        Sprite,
        Matcap,
        MobileToon,
        Hidden
    }

    /// <summary>
    /// VRChat Custom Safety Fallback のレンダリングモード
    /// </summary>
    public enum VRCFallbackRenderingMode
    {
        Opaque,
        Cutout,
        Transparent,
        Fade
    }

    /// <summary>
    /// VRChat Custom Safety Fallback のカリングモード
    /// </summary>
    public enum VRCFallbackFacing
    {
        Default,
        DoubleSided
    }
}
