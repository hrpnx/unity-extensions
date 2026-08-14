using System;
using System.Globalization;
using System.IO;
using Hrpnx.UnityExtensions.CheekPuffResetter;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(Hrpnx.UnityExtensions.CheekPuffResetter.CheekPuffResetterPlugin))]

namespace Hrpnx.UnityExtensions.CheekPuffResetter
{
    /// <summary>
    /// CheekPuffLeft/Right PhysBone リセットギミックを FX レイヤーに組み込む NDMF プラグイン
    /// </summary>
    public class CheekPuffResetterPlugin : Plugin<CheekPuffResetterPlugin>
    {
        private const string PluginName = "CheekPuffResetter";
        private const string ParamLeft = "CheekPuffLeft";
        private const string ParamRight = "CheekPuffRight";
        private const string CacheKeyFileName = ".cachekey";
        private const string EnableClipLeftName = "Enable_L.anim";
        private const string DisableClipLeftName = "Disable_L.anim";
        private const string EnableClipRightName = "Enable_R.anim";
        private const string DisableClipRightName = "Disable_R.anim";
        private const string ControllerName = "CheekPuffReset_Controller.controller";

        public override string QualifiedName => "dev.hrpnx.cheekpuff-resetter";
        public override string DisplayName => "CheekPuff Resetter";

        protected override void Configure() =>
            this.InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run(
                    "Install CheekPuff Resetter",
                    ctx =>
                    {
                        var resetter =
                            ctx.AvatarRootObject.GetComponentInChildren<CheekPuffResetter>();
                        if (resetter == null)
                        {
                            return;
                        }

                        InstallResetter(ctx.AvatarRootObject, resetter);
                    }
                );

        private static void InstallResetter(GameObject avatarRoot, CheekPuffResetter resetter)
        {
            if (resetter.CheekBoneL == null || resetter.CheekBoneR == null)
            {
                Debug.LogWarning(
                    $"[{PluginName}] CheekBoneL または CheekBoneR が未設定です。スキップします。"
                );
                return;
            }

            var physBoneL = resetter.CheekBoneL.GetComponent<VRCPhysBoneBase>();
            var physBoneR = resetter.CheekBoneR.GetComponent<VRCPhysBoneBase>();

            if (physBoneL == null)
            {
                Debug.LogWarning(
                    $"[{PluginName}] CheekBoneL ({resetter.CheekBoneL.name}) に VRCPhysBone が見つかりません。スキップします。"
                );
                return;
            }

            if (physBoneR == null)
            {
                Debug.LogWarning(
                    $"[{PluginName}] CheekBoneR ({resetter.CheekBoneR.name}) に VRCPhysBone が見つかりません。スキップします。"
                );
                return;
            }

            string pathL = GetRelativePath(resetter.CheekBoneL, avatarRoot.transform);
            string pathR = GetRelativePath(resetter.CheekBoneR, avatarRoot.transform);

            if (pathL == null || pathR == null)
            {
                Debug.LogWarning(
                    $"[{PluginName}] CheekBone の相対パスを計算できません。アバター直下の階層に配置されているか確認してください。"
                );
                return;
            }

            var typeL = physBoneL.GetType();
            var typeR = physBoneR.GetType();
            string cacheKey = BuildCacheKey(pathL, pathR, typeL, typeR, resetter.Threshold);

            // 入力が前回と同一なら生成をまるごと省き、既存アセットを再利用する
            var controller =
                TryLoadCachedController(cacheKey)
                ?? GenerateAssets(pathL, pathR, typeL, typeR, resetter.Threshold, cacheKey);

            AttachModularAvatarComponents(resetter, controller);
        }

        /// <summary>
        /// アセット生成を 1 バッチにまとめて実行する。
        /// CreateAsset は 1 件ごとに同期インポートが走るため、StartAssetEditing で必ず束ねる。
        /// </summary>
        private static AnimatorController GenerateAssets(
            string pathL,
            string pathR,
            Type typeL,
            Type typeR,
            float threshold,
            string cacheKey
        )
        {
            string assetDir = GetGeneratedAssetsRelativeDirectory();
            PrepareGeneratedDirectory();

            AnimatorController controller;
            AssetDatabase.StartAssetEditing();
            try
            {
                var enableClipL = CreatePhysBoneClip(pathL, typeL, enabled: true);
                CreateAsset(enableClipL, $"{assetDir}/{EnableClipLeftName}");

                var disableClipL = CreatePhysBoneClip(pathL, typeL, enabled: false);
                CreateAsset(disableClipL, $"{assetDir}/{DisableClipLeftName}");

                var enableClipR = CreatePhysBoneClip(pathR, typeR, enabled: true);
                CreateAsset(enableClipR, $"{assetDir}/{EnableClipRightName}");

                var disableClipR = CreatePhysBoneClip(pathR, typeR, enabled: false);
                CreateAsset(disableClipR, $"{assetDir}/{DisableClipRightName}");

                controller = CreateAnimatorController(
                    threshold,
                    enableClipL,
                    disableClipL,
                    enableClipR,
                    disableClipR
                );
                CreateAsset(controller, $"{assetDir}/{ControllerName}");

                WriteCacheKey(cacheKey);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return controller;
        }

        /// <summary>
        /// PhysBone の enabled アニメーションクリップを生成する。
        /// disable クリップは 2 フレーム分のキーフレームを持ち、確実に 1 フレーム以上無効化される。
        /// </summary>
        private static AnimationClip CreatePhysBoneClip(
            string path,
            Type componentType,
            bool enabled
        )
        {
            var clip = new AnimationClip { frameRate = 60f };
            float value = enabled ? 1f : 0f;

            var binding = new EditorCurveBinding
            {
                path = path,
                type = componentType,
                propertyName = "m_Enabled",
            };

            var curve = new AnimationCurve();
            // 定数カーブにするため tangent を無限大に設定
            curve.AddKey(new Keyframe(0f, value, float.PositiveInfinity, float.PositiveInfinity));

            if (!enabled)
            {
                // リセット用クリップはクリップ長を 2 フレーム分確保する
                curve.AddKey(
                    new Keyframe(2f / 60f, value, float.PositiveInfinity, float.PositiveInfinity)
                );
            }

            AnimationUtility.SetEditorCurve(clip, binding, curve);
            return clip;
        }

        private static AnimatorController CreateAnimatorController(
            float threshold,
            AnimationClip enableClipL,
            AnimationClip disableClipL,
            AnimationClip enableClipR,
            AnimationClip disableClipR
        )
        {
            var controller = new AnimatorController();

            controller.AddParameter(
                new AnimatorControllerParameter
                {
                    name = ParamLeft,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 0f,
                }
            );
            controller.AddParameter(
                new AnimatorControllerParameter
                {
                    name = ParamRight,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 0f,
                }
            );

            controller.AddLayer("CheekReset_L");
            controller.AddLayer("CheekReset_R");

            // AddLayer で追加した 2 層目以降は defaultWeight = 0 のため書き戻す
            var layers = controller.layers;
            layers[0].defaultWeight = 1f;
            layers[1].defaultWeight = 1f;
            controller.layers = layers;

            SetupResetLayer(
                layers[0].stateMachine,
                ParamLeft,
                threshold,
                enableClipL,
                disableClipL
            );
            SetupResetLayer(
                layers[1].stateMachine,
                ParamRight,
                threshold,
                enableClipR,
                disableClipR
            );

            return controller;
        }

        /// <summary>
        /// 1 レイヤー分のステートマシンを構築する。
        ///
        /// Idle (有効) → Resetting (無効化、クリップ再生完了まで待機) → WaitRelease (再有効化) → Idle (パラメータが閾値未満)
        /// </summary>
        private static void SetupResetLayer(
            AnimatorStateMachine stateMachine,
            string paramName,
            float threshold,
            AnimationClip enableClip,
            AnimationClip disableClip
        )
        {
            stateMachine.entryPosition = new Vector3(-200f, 0f);
            stateMachine.anyStatePosition = new Vector3(-200f, 100f);
            stateMachine.exitPosition = new Vector3(-200f, -100f);

            var idle = stateMachine.AddState("Idle", new Vector3(100f, 0f));
            idle.motion = enableClip;
            idle.writeDefaultValues = false;

            var resetting = stateMachine.AddState("Resetting", new Vector3(100f, 120f));
            resetting.motion = disableClip;
            resetting.writeDefaultValues = false;

            var waitRelease = stateMachine.AddState("WaitRelease", new Vector3(100f, 240f));
            waitRelease.motion = enableClip;
            waitRelease.writeDefaultValues = false;

            stateMachine.defaultState = idle;

            // Idle → Resetting: CheekPuffLeft/Right が閾値を超えたらリセット開始
            var toResetting = idle.AddTransition(resetting);
            toResetting.AddCondition(AnimatorConditionMode.Greater, threshold, paramName);
            toResetting.hasExitTime = false;
            toResetting.duration = 0f;

            // Resetting → WaitRelease: disable クリップを 1 回再生し終えたら遷移 (PhysBone が確実に無効化される)
            var toWaitRelease = resetting.AddTransition(waitRelease);
            toWaitRelease.hasExitTime = true;
            toWaitRelease.exitTime = 1.0f;
            toWaitRelease.duration = 0f;

            // WaitRelease → Idle: パラメータが閾値未満に戻ったら待機解除
            var toIdle = waitRelease.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.Less, threshold, paramName);
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;
        }

        private static void AttachModularAvatarComponents(
            CheekPuffResetter resetter,
            AnimatorController controller
        )
        {
            var gameObject = resetter.gameObject;

            var parameters = gameObject.AddComponent<ModularAvatarParameters>();

            parameters.parameters.Add(
                new ParameterConfig
                {
                    nameOrPrefix = ParamLeft,
                    defaultValue = 0f,
                    saved = false,
                    syncType = ParameterSyncType.Float,
                }
            );
            parameters.parameters.Add(
                new ParameterConfig
                {
                    nameOrPrefix = ParamRight,
                    defaultValue = 0f,
                    saved = false,
                    syncType = ParameterSyncType.Float,
                }
            );

            var mergeAnimator = gameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = controller;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = false;
            mergeAnimator.layerPriority = 1;
        }

        private static string GetRelativePath(Transform target, Transform root)
        {
            if (target == root)
            {
                return string.Empty;
            }

            string path = target.name;
            var parent = target.parent;
            while (parent != null && parent != root)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return parent == root ? path : null;
        }

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

        /// <summary>
        /// 生成物を左右する入力 (ボーンの相対パス・PhysBone の型・閾値) からキャッシュキーを作る。
        /// </summary>
        private static string BuildCacheKey(
            string pathL,
            string pathR,
            Type typeL,
            Type typeR,
            float threshold
        ) =>
            string.Join(
                "\n",
                "v1",
                pathL,
                pathR,
                typeL.FullName,
                typeR.FullName,
                threshold.ToString("R", CultureInfo.InvariantCulture)
            );

        /// <summary>
        /// キャッシュキーが一致し、生成アセットが 5 件すべて健在なら AnimatorController を返す。
        /// </summary>
        private static AnimatorController TryLoadCachedController(string cacheKey)
        {
            if (!MatchesCacheKey(cacheKey))
            {
                return null;
            }

            string assetDir = GetGeneratedAssetsRelativeDirectory();
            var clipNames = new[]
            {
                EnableClipLeftName,
                DisableClipLeftName,
                EnableClipRightName,
                DisableClipRightName,
            };

            foreach (string clipName in clipNames)
            {
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>($"{assetDir}/{clipName}") == null)
                {
                    return null;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimatorController>(
                $"{assetDir}/{ControllerName}"
            );
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

        private static string GetGeneratedAssetsAbsoluteDirectory()
        {
            string packagePath = Path.GetFullPath("Packages/dev.hrpnx.unity-extensions");
            return Path.Combine(packagePath, "__Generated", PluginName);
        }

        private static string GetGeneratedAssetsRelativeDirectory() =>
            $"Packages/dev.hrpnx.unity-extensions/__Generated/{PluginName}";

        private static void CreateAsset(UnityEngine.Object asset, string dest)
        {
            if (File.Exists(dest))
            {
                AssetDatabase.DeleteAsset(dest);
            }

            AssetDatabase.CreateAsset(asset, dest);
        }
    }
}
