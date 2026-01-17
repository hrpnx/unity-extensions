using System;
using System.Collections.Generic;
using System.IO;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using ExpressionControl = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;

[assembly: ExportsPlugin(
    typeof(Hrpnx.UnityExtensions.BackLitMenuInstaller.BackLitMenuInstallerPlugin)
)]

namespace Hrpnx.UnityExtensions.BackLitMenuInstaller
{
    /// <summary>
    /// ビルド時に lilToon の BackLit メニューを自動生成する NDMF プラグイン
    /// </summary>
    public class BackLitMenuInstallerPlugin : Plugin<BackLitMenuInstallerPlugin>
    {
        private const string BaseName = "BackLit";

        public override string QualifiedName => "dev.hrpnx.backlit-menu-installer";
        public override string DisplayName => "BackLit Menu Installer";

        protected override void Configure() =>
            this.InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run(
                    "Install BackLit Menu",
                    ctx =>
                    {
                        var installer =
                            ctx.AvatarRootObject.GetComponentInChildren<BackLitMenuInstaller>();
                        if (installer == null)
                        {
                            return;
                        }

                        var avatarRoot = installer.gameObject.transform.parent?.gameObject;
                        if (avatarRoot == null)
                        {
                            Debug.LogError(
                                $"BackLitMenuInstaller component on '{installer.gameObject.name}' has no parent object. Please place it as a child of the avatar root."
                            );
                            return;
                        }

                        if (!avatarRoot.GetComponent<VRCAvatarDescriptor>())
                        {
                            Debug.LogError(
                                $"Parent object '{avatarRoot.name}' does not have VRCAvatarDescriptor component. BackLitMenuInstaller must be placed under the avatar root."
                            );
                            return;
                        }

                        CreateMenu(avatarRoot, installer);
                    }
                );

        private void CreateMenu(GameObject avatarRoot, BackLitMenuInstaller installer)
        {
            var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);
            string absoluteDir = GetGeneratedAssetsAbsoluteDirectory();
            string assetDir = GetGeneratedAssetsRelativeDirectory();

            if (Directory.Exists(absoluteDir))
            {
                Directory.Delete(absoluteDir, true);
            }
            Directory.CreateDirectory(absoluteDir);
            AssetDatabase.Refresh();

            var animOnClip = CreateOnAnimationClip(renderers, avatarRoot.transform, installer);
            CreateAsset(animOnClip, $"{assetDir}/{BaseName}_On.anim");

            var animOffClip = CreateOffAnimationClip(
                renderers,
                avatarRoot.transform,
                installer.Exclusions
            );
            CreateAsset(animOffClip, $"{assetDir}/{BaseName}_Off.anim");

            var controller = CreateAnimatorController(animOnClip, animOffClip);
            CreateAsset(controller, $"{assetDir}/{BaseName}_Controller.controller");

            var menu = CreateExpressionsMenu();
            CreateAsset(menu, $"{assetDir}/{BaseName}_Menu.asset");

            AttachModularAvatarComponents(installer, controller, menu);
        }

        private static string GetGeneratedAssetsAbsoluteDirectory()
        {
            string packagePath = Path.GetFullPath("Packages/dev.hrpnx.unity-extensions");
            return Path.Combine(packagePath, "__Generated", "BackLitMenuInstaller");
        }

        private static string GetGeneratedAssetsRelativeDirectory() =>
            "Packages/dev.hrpnx.unity-extensions/__Generated/BackLitMenuInstaller";

        private AnimationClip CreateOnAnimationClip(
            Renderer[] renderers,
            Transform rootTransform,
            BackLitMenuInstaller installer
        )
        {
            var clip = new AnimationClip();

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var transform = renderer.gameObject.transform;
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._UseBacklight",
                    1,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightColor.r",
                    installer.Color.r,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightColor.g",
                    installer.Color.g,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightColor.b",
                    installer.Color.b,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightColor.a",
                    installer.Color.a,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightMainStrength",
                    installer.MainStrength,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightNormalStrength",
                    installer.NormalStrength,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightBorder",
                    installer.Border,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightBlur",
                    installer.Blur,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightDirectivity",
                    installer.Directivity,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightViewStrength",
                    installer.ViewStrength,
                    installer.Exclusions
                );
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._BacklightReceiveShadow",
                    installer.ReceiveShadow,
                    installer.Exclusions
                );
            }

            return clip;
        }

        private AnimationClip CreateOffAnimationClip(
            Renderer[] renderers,
            Transform rootTransform,
            List<Material> exclusions
        )
        {
            var clip = new AnimationClip();

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var transform = renderer.gameObject.transform;
                AddAnimation(
                    transform,
                    rootTransform,
                    clip,
                    "material._UseBacklight",
                    0,
                    exclusions
                );
            }

            return clip;
        }

        private AnimatorController CreateAnimatorController(
            AnimationClip onClip,
            AnimationClip offClip
        )
        {
            var controller = new AnimatorController();
            controller.AddParameter(
                new AnimatorControllerParameter
                {
                    name = BaseName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = false,
                }
            );
            controller.AddLayer(BaseName);

            var layer = controller.layers[0];
            layer.name = BaseName;
            layer.stateMachine.name = BaseName;
            layer.stateMachine.entryPosition = new Vector3(0, 0);
            layer.stateMachine.anyStatePosition = new Vector3(300, 0);
            layer.stateMachine.exitPosition = new Vector3(0, -75);

            var offState = layer.stateMachine.AddState($"{BaseName}_Off", new Vector3(150, 150));
            offState.motion = offClip;
            offState.writeDefaultValues = false;

            var toOffTransition = layer.stateMachine.AddAnyStateTransition(offState);
            toOffTransition.AddCondition(AnimatorConditionMode.IfNot, 0, BaseName);
            toOffTransition.hasExitTime = false;
            toOffTransition.duration = 0f;

            var onState = layer.stateMachine.AddState($"{BaseName}_On", new Vector3(150, -150));
            onState.motion = onClip;
            onState.writeDefaultValues = false;

            var toOnTransition = layer.stateMachine.AddAnyStateTransition(onState);
            toOnTransition.AddCondition(AnimatorConditionMode.If, 0, BaseName);
            toOnTransition.hasExitTime = false;
            toOnTransition.duration = 0f;

            return controller;
        }

        private static VRCExpressionsMenu CreateExpressionsMenu()
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.name = BaseName;

            var control = new ExpressionControl
            {
                name = BaseName,
                type = ExpressionControl.ControlType.Toggle,
                value = 1,
                parameter = new ExpressionControl.Parameter { name = BaseName },
            };
            menu.controls.Add(control);

            return menu;
        }

        private static void AttachModularAvatarComponents(
            BackLitMenuInstaller installer,
            AnimatorController controller,
            VRCExpressionsMenu menu
        )
        {
            var gameObject = installer.gameObject;

            var menuInstaller = gameObject.AddComponent<ModularAvatarMenuInstaller>();
            if (installer.RootMenu != null)
            {
                menuInstaller.installTargetMenu = installer.RootMenu;
            }
            menuInstaller.menuToAppend = menu;

            var parameters = gameObject.AddComponent<ModularAvatarParameters>();
            parameters.parameters.Add(
                new ParameterConfig
                {
                    nameOrPrefix = BaseName,
                    defaultValue = installer.Default ? 1 : 0,
                    saved = installer.Saved,
                    syncType = ParameterSyncType.Bool,
                }
            );

            var mergeAnimator = gameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = controller;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = false;
            mergeAnimator.layerPriority = 1;
        }

        private void AddAnimation(
            Transform transform,
            Transform rootTransform,
            AnimationClip clip,
            string propertyName,
            float value,
            List<Material> exclusions
        )
        {
            string path = GetRelativePath(transform, rootTransform);
            if (path == null)
            {
                return;
            }

            var renderer = transform.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            foreach (var material in renderer.sharedMaterials)
            {
                if (
                    material == null
                    || !material.shader.name.Contains("lilToon")
                    || (exclusions != null && exclusions.Contains(material))
                )
                {
                    continue;
                }

                SetCurve(clip, propertyName, value, path, renderer.GetType());
            }
        }

        private static string GetRelativePath(Transform transform, Transform root)
        {
            if (transform == root)
            {
                return string.Empty;
            }

            string path = transform.name;
            var parent = transform.parent;
            while (parent != null && parent != root)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return parent == root ? path : null;
        }

        private static void SetCurve(
            AnimationClip clip,
            string propertyName,
            float value,
            string path,
            Type type
        )
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!binding.propertyName.StartsWith(propertyName) || binding.path != path)
                {
                    continue;
                }

                var editorCurve = AnimationUtility.GetEditorCurve(clip, binding);
                editorCurve.AddKey(0, value);
                AnimationUtility.SetEditorCurve(clip, binding, editorCurve);
                return;
            }

            var curveBinding = new EditorCurveBinding
            {
                path = path,
                type = type,
                propertyName = propertyName,
            };

            var curve = new AnimationCurve();
            curve.AddKey(0, value);
            AnimationUtility.SetEditorCurve(clip, curveBinding, curve);
        }

        private static void CreateAsset(UnityEngine.Object asset, string dest)
        {
            if (File.Exists(dest))
            {
                AssetDatabase.DeleteAsset(dest);
            }

            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(dest));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
