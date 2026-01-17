using Hrpnx.UnityExtensions.VRCFallbackSetter;
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(VRCFallbackSetterPlugin))]

namespace Hrpnx.UnityExtensions.VRCFallbackSetter
{
    /// <summary>
    /// ビルド時にアバター配下の全マテリアルの _VRCFallback タグを設定する NDMF プラグイン
    /// </summary>
    public class VRCFallbackSetterPlugin : Plugin<VRCFallbackSetterPlugin>
    {
        public override string QualifiedName => "dev.hrpnx.vrc-fallback-setter";
        public override string DisplayName => "VRC Fallback Setter";

        protected override void Configure() => this
            .InPhase(BuildPhase.Transforming)
            .BeforePlugin("nadena.dev.modular-avatar")
            .Run("Set VRCFallback", ctx =>
            {
                var setter = ctx.AvatarRootObject.GetComponentInChildren<VRCFallbackSetter>();
                if (setter == null)
                {
                    return;
                }

                var avatarRoot = setter.gameObject.transform.parent?.gameObject;
                if (avatarRoot == null)
                {
                    Debug.LogError($"VRCFallbackSetter component on '{setter.gameObject.name}' has no parent object. Please place it as a child of the avatar root.");
                    return;
                }

                if (!avatarRoot.GetComponent<VRCAvatarDescriptor>())
                {
                    Debug.LogError($"Parent object '{avatarRoot.name}' does not have VRCAvatarDescriptor component. VRCFallbackSetter must be placed under the avatar root.");
                    return;
                }

                SetFallback(avatarRoot, setter);
            });

        private void SetFallback(GameObject avatarRoot, VRCFallbackSetter setter)
        {
            var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);
            int processedCount = 0;
            int excludedCount = 0;
            string fallbackValue = BuildFallbackTag(setter);

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    if (setter.Exclusions != null && setter.Exclusions.Contains(material))
                    {
                        excludedCount++;
                        continue;
                    }

                    material.SetOverrideTag("VRCFallback", fallbackValue);
                    processedCount++;
                }
            }

            Debug.Log($"[VRCFallbackSetter] Set VRCFallback to \"{fallbackValue}\" for {processedCount} materials. (Excluded: {excludedCount})");
        }

        private static string BuildFallbackTag(VRCFallbackSetter setter)
        {
            string tag = setter.ShaderType.ToString();

            tag += setter.RenderingMode switch
            {
                VRCFallbackRenderingMode.Opaque => string.Empty,
                VRCFallbackRenderingMode.Cutout => "Cutout",
                VRCFallbackRenderingMode.Transparent => "Transparent",
                VRCFallbackRenderingMode.Fade => "Fade",
                _ => string.Empty
            };

            tag += setter.Facing switch
            {
                VRCFallbackFacing.Default => string.Empty,
                VRCFallbackFacing.DoubleSided => "DoubleSided",
                _ => string.Empty
            };

            return tag;
        }
    }
}
