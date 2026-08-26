using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        private const string CacheKeyFileName = ".cachekey";

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

                        CreateMenu(ctx.AvatarRootObject, installer);
                    }
                );

        private static void CreateMenu(GameObject avatarRoot, BackLitMenuInstaller installer)
        {
            var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);
            string cacheKey = BuildCacheKey(renderers, avatarRoot.transform, installer);

            // 入力が前回と同一なら生成をまるごと省き、既存アセットを再利用する
            if (!TryLoadCachedAssets(cacheKey, out var controller, out var menu))
            {
                GenerateAssets(
                    renderers,
                    avatarRoot.transform,
                    installer,
                    cacheKey,
                    out controller,
                    out menu
                );
            }

            AttachModularAvatarComponents(installer, controller, menu);
        }

        /// <summary>
        /// アセット生成を 1 バッチにまとめて実行する。
        /// CreateAsset は 1 件ごとに同期インポートが走るため、StartAssetEditing で必ず束ねる。
        /// </summary>
        private static void GenerateAssets(
            Renderer[] renderers,
            Transform rootTransform,
            BackLitMenuInstaller installer,
            string cacheKey,
            out AnimatorController controller,
            out VRCExpressionsMenu menu
        )
        {
            controller = null;
            menu = null;

            string assetDir = GetGeneratedAssetsRelativeDirectory();
            PrepareGeneratedDirectory();

            AssetDatabase.StartAssetEditing();
            try
            {
                var animOnClip = CreateOnAnimationClip(renderers, rootTransform, installer);
                CreateAsset(animOnClip, $"{assetDir}/{BaseName}_On.anim");

                var animOffClip = CreateOffAnimationClip(
                    renderers,
                    rootTransform,
                    installer.ExcludedRenderers
                );
                CreateAsset(animOffClip, $"{assetDir}/{BaseName}_Off.anim");

                // AnimatorController は先にアセット化してから中身を組む。
                // AddLayer / AddState / AddAnyStateTransition は
                // AssetDatabase.GetAssetPath(controller) が空でないときだけ
                // ステートマシン等をサブアセットとして登録するため、
                // メモリ上で組み立ててから CreateAsset すると m_StateMachine が
                // {fileID: 0} のまま保存され、レイヤーが空の .controller になる。
                controller = new AnimatorController();
                CreateAsset(controller, $"{assetDir}/{BaseName}_Controller.controller");
                BuildAnimatorController(controller, animOnClip, animOffClip);

                menu = CreateExpressionsMenu();
                CreateAsset(menu, $"{assetDir}/{BaseName}_Menu.asset");

                WriteCacheKey(cacheKey);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string GetGeneratedAssetsAbsoluteDirectory()
        {
            string packagePath = Path.GetFullPath("Packages/dev.hrpnx.unity-extensions");
            return Path.Combine(packagePath, "__Generated", "BackLitMenuInstaller");
        }

        private static string GetGeneratedAssetsRelativeDirectory() =>
            "Packages/dev.hrpnx.unity-extensions/__Generated/BackLitMenuInstaller";

        /// <summary>
        /// 生成先フォルダを用意する。
        /// CreateAsset は AssetDatabase に登録済みのフォルダを要求するため、未登録のときだけ Refresh する。
        /// </summary>
        private static void PrepareGeneratedDirectory()
        {
            string absoluteDir = GetGeneratedAssetsAbsoluteDirectory();
            if (!Directory.Exists(absoluteDir))
            {
                Directory.CreateDirectory(absoluteDir);
            }

            if (!AssetDatabase.IsValidFolder(GetGeneratedAssetsRelativeDirectory()))
            {
                AssetDatabase.Refresh();
            }
        }

        private static AnimationClip CreateOnAnimationClip(
            Renderer[] renderers,
            Transform rootTransform,
            BackLitMenuInstaller installer
        )
        {
            var clip = new AnimationClip();
            var buffer = new CurveBuffer();

            foreach (var renderer in renderers)
            {
                if (
                    !TryResolveAnimationTarget(
                        renderer,
                        rootTransform,
                        installer.ExcludedRenderers,
                        out string path,
                        out var type
                    )
                )
                {
                    continue;
                }

                AddOnCurves(buffer, path, type, installer);
            }

            buffer.ApplyTo(clip);
            return clip;
        }

        private static void AddOnCurves(
            CurveBuffer buffer,
            string path,
            Type type,
            BackLitMenuInstaller installer
        )
        {
            // 並びは lilToon のインスペクタ (逆光ライト) に合わせている
            buffer.Add(path, type, "material._UseBacklight", 1);
            buffer.Add(path, type, "material._BacklightColor.r", installer.Color.r);
            buffer.Add(path, type, "material._BacklightColor.g", installer.Color.g);
            buffer.Add(path, type, "material._BacklightColor.b", installer.Color.b);
            buffer.Add(path, type, "material._BacklightColor.a", installer.Color.a);
            buffer.Add(path, type, "material._BacklightMainStrength", installer.MainStrength);
            buffer.Add(
                path,
                type,
                "material._BacklightReceiveShadow",
                installer.ReceiveShadow ? 1 : 0
            );
            buffer.Add(
                path,
                type,
                "material._BacklightBackfaceMask",
                installer.BackfaceMask ? 1 : 0
            );
            buffer.Add(path, type, "material._BacklightNormalStrength", installer.NormalStrength);
            // lilToon は _BacklightBorder を 1 - value で「範囲」として表示する
            // (lilEditorGUI.InvBorderGUI)。Border にはその表示値を持たせているので反転して書く。
            buffer.Add(path, type, "material._BacklightBorder", 1f - installer.Border);
            buffer.Add(path, type, "material._BacklightBlur", installer.Blur);
            buffer.Add(path, type, "material._BacklightDirectivity", installer.Directivity);
            buffer.Add(path, type, "material._BacklightViewStrength", installer.ViewStrength);
        }

        private static AnimationClip CreateOffAnimationClip(
            Renderer[] renderers,
            Transform rootTransform,
            List<Renderer> excludedRenderers
        )
        {
            var clip = new AnimationClip();
            var buffer = new CurveBuffer();

            foreach (var renderer in renderers)
            {
                if (
                    !TryResolveAnimationTarget(
                        renderer,
                        rootTransform,
                        excludedRenderers,
                        out string path,
                        out var type
                    )
                )
                {
                    continue;
                }

                buffer.Add(path, type, "material._UseBacklight", 0);
            }

            buffer.ApplyTo(clip);
            return clip;
        }

        /// <summary>
        /// アセット化済みの AnimatorController にレイヤーとステートを組み立てる。
        /// </summary>
        private static void BuildAnimatorController(
            AnimatorController controller,
            AnimationClip onClip,
            AnimationClip offClip
        )
        {
            controller.AddParameter(
                new AnimatorControllerParameter
                {
                    name = BaseName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = false,
                }
            );
            controller.AddLayer(BaseName);

            var layers = controller.layers;
            var layer = layers[0];
            layer.name = BaseName;

            // AddLayer(string) の既定の defaultWeight は 0。
            // 0 のままだとレイヤーの出力が一切合成されず、アニメーションが反映されない。
            layer.defaultWeight = 1f;

            var stateMachine = layer.stateMachine;
            stateMachine.name = BaseName;
            stateMachine.entryPosition = new Vector3(0, 0);
            stateMachine.anyStatePosition = new Vector3(300, 0);
            stateMachine.exitPosition = new Vector3(0, -75);

            var offState = stateMachine.AddState($"{BaseName}_Off", new Vector3(150, 150));
            offState.motion = offClip;
            offState.writeDefaultValues = false;

            var toOffTransition = stateMachine.AddAnyStateTransition(offState);
            toOffTransition.AddCondition(AnimatorConditionMode.IfNot, 0, BaseName);
            toOffTransition.hasExitTime = false;
            toOffTransition.duration = 0f;
            toOffTransition.canTransitionToSelf = false;

            var onState = stateMachine.AddState($"{BaseName}_On", new Vector3(150, -150));
            onState.motion = onClip;
            onState.writeDefaultValues = false;

            var toOnTransition = stateMachine.AddAnyStateTransition(onState);
            toOnTransition.AddCondition(AnimatorConditionMode.If, 0, BaseName);
            toOnTransition.hasExitTime = false;
            toOnTransition.duration = 0f;
            toOnTransition.canTransitionToSelf = false;

            // controller.layers はコピーを返すため、書き戻さないと
            // defaultWeight / name の変更が破棄される。
            controller.layers = layers;
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

        /// <summary>
        /// アニメーション対象の Renderer かを判定し、対象なら相対パスと Renderer の型を返す。
        /// </summary>
        private static bool TryResolveAnimationTarget(
            Renderer sourceRenderer,
            Transform rootTransform,
            List<Renderer> excludedRenderers,
            out string path,
            out Type rendererType
        )
        {
            path = null;
            rendererType = null;

            if (sourceRenderer == null)
            {
                return false;
            }

            string relativePath = GetRelativePath(
                sourceRenderer.gameObject.transform,
                rootTransform
            );
            if (relativePath == null)
            {
                return false;
            }

            var renderer = sourceRenderer.gameObject.transform.GetComponent<Renderer>();
            if (renderer == null)
            {
                return false;
            }

            if (excludedRenderers != null && excludedRenderers.Contains(renderer))
            {
                return false;
            }

            if (!HasTargetShader(renderer))
            {
                return false;
            }

            path = relativePath;
            rendererType = renderer.GetType();
            return true;
        }

        private static bool HasTargetShader(Renderer renderer)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null)
                {
                    continue;
                }

                string shaderName = material.shader.name;
                if (shaderName.Contains("lilToon") || shaderName.Contains("lilSSRT"))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// 生成物を左右する入力すべて (installer のパラメータ + Renderer 構成) からキャッシュキーを作る。
        /// </summary>
        private static string BuildCacheKey(
            Renderer[] renderers,
            Transform rootTransform,
            BackLitMenuInstaller installer
        )
        {
            var builder = new StringBuilder();
            AppendParameters(builder, installer);
            AppendRenderers(builder, renderers, rootTransform, installer.ExcludedRenderers);
            return ToSha256Hex(builder.ToString());
        }

        private static void AppendParameters(StringBuilder builder, BackLitMenuInstaller installer)
        {
            var color = installer.Color;
            AppendFloat(builder, color.r);
            AppendFloat(builder, color.g);
            AppendFloat(builder, color.b);
            AppendFloat(builder, color.a);
            AppendFloat(builder, installer.MainStrength);
            builder.Append(installer.ReceiveShadow ? '1' : '0').Append('|');
            builder.Append(installer.BackfaceMask ? '1' : '0').Append('|');
            AppendFloat(builder, installer.NormalStrength);
            AppendFloat(builder, installer.Border);
            AppendFloat(builder, installer.Blur);
            AppendFloat(builder, installer.Directivity);
            AppendFloat(builder, installer.ViewStrength);
            builder.Append(installer.Default ? '1' : '0').Append('|');
            builder.Append(installer.Saved ? '1' : '0').Append('|');
            builder
                .Append(
                    installer.RootMenu == null
                        ? string.Empty
                        : AssetDatabase.GetAssetPath(installer.RootMenu)
                )
                .Append('\n');
        }

        private static void AppendRenderers(
            StringBuilder builder,
            Renderer[] renderers,
            Transform rootTransform,
            List<Renderer> excludedRenderers
        )
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    builder.Append("-|-|0\n");
                    continue;
                }

                bool isTarget = TryResolveAnimationTarget(
                    renderer,
                    rootTransform,
                    excludedRenderers,
                    out _,
                    out _
                );
                builder
                    .Append(GetRelativePath(renderer.gameObject.transform, rootTransform) ?? "-")
                    .Append('|')
                    .Append(renderer.GetType().FullName)
                    .Append('|')
                    .Append(isTarget ? '1' : '0')
                    .Append('\n');
            }
        }

        private static void AppendFloat(StringBuilder builder, float value) =>
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('|');

        private static string ToSha256Hex(string source)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(source));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// キャッシュキーが一致し、生成アセットが 4 件すべて健在なら再利用する。
        /// </summary>
        private static bool TryLoadCachedAssets(
            string cacheKey,
            out AnimatorController controller,
            out VRCExpressionsMenu menu
        )
        {
            controller = null;
            menu = null;

            if (!MatchesCacheKey(cacheKey))
            {
                return false;
            }

            string assetDir = GetGeneratedAssetsRelativeDirectory();
            var cachedOnClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"{assetDir}/{BaseName}_On.anim"
            );
            var cachedOffClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"{assetDir}/{BaseName}_Off.anim"
            );
            var cachedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                $"{assetDir}/{BaseName}_Controller.controller"
            );
            var cachedMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(
                $"{assetDir}/{BaseName}_Menu.asset"
            );

            if (
                cachedOnClip == null
                || cachedOffClip == null
                || cachedController == null
                || cachedMenu == null
            )
            {
                return false;
            }

            controller = cachedController;
            menu = cachedMenu;
            return true;
        }

        private static string GetCacheKeyPath() =>
            Path.Combine(GetGeneratedAssetsAbsoluteDirectory(), CacheKeyFileName);

        private static bool MatchesCacheKey(string cacheKey)
        {
            string keyPath = GetCacheKeyPath();
            return File.Exists(keyPath) && File.ReadAllText(keyPath) == cacheKey;
        }

        private static void WriteCacheKey(string cacheKey) =>
            File.WriteAllText(GetCacheKeyPath(), cacheKey);

        private static void CreateAsset(UnityEngine.Object asset, string dest)
        {
            if (File.Exists(dest))
            {
                AssetDatabase.DeleteAsset(dest);
            }

            AssetDatabase.CreateAsset(asset, dest);
        }

        /// <summary>
        /// カーブを溜めてから SetEditorCurves で一括適用するバッファ。
        /// GetCurveBindings の線形走査 (O(n^2)) を避けるため、重複は (path, propertyName) の集合で弾く。
        /// </summary>
        private sealed class CurveBuffer
        {
            private readonly List<EditorCurveBinding> _bindings = new List<EditorCurveBinding>();
            private readonly List<AnimationCurve> _curves = new List<AnimationCurve>();
            private readonly HashSet<string> _registered = new HashSet<string>();

            public void Add(string path, Type type, string propertyName, float value)
            {
                // 既に登録済みなら何もしない (旧実装も時刻 0 のキー再追加で実質 no-op だった)
                if (!_registered.Add($"{path}\0{propertyName}"))
                {
                    return;
                }

                _bindings.Add(
                    new EditorCurveBinding
                    {
                        path = path,
                        type = type,
                        propertyName = propertyName,
                    }
                );

                var curve = new AnimationCurve();
                curve.AddKey(0, value);
                _curves.Add(curve);
            }

            public void ApplyTo(AnimationClip clip)
            {
                if (_bindings.Count == 0)
                {
                    return;
                }

                AnimationUtility.SetEditorCurves(clip, _bindings.ToArray(), _curves.ToArray());
            }
        }
    }
}
