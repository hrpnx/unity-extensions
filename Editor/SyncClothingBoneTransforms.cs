using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Hrpnx.UnityExtensions
{
    public static class SyncClothingBoneTransforms
    {
        [MenuItem("GameObject/Sync Clothing Bone Transforms from Avatar", false, 49)]
        public static void SyncBoneTransforms()
        {
            var clothing = Selection.activeGameObject;

            var avatarDescriptor = clothing.GetComponentInParent<VRCAvatarDescriptor>();
            if (avatarDescriptor == null)
            {
                EditorUtility.DisplayDialog(
                    "Sync Clothing Bone Transforms",
                    "選択オブジェクトの親階層に VRC Avatar Descriptor が見つかりません。",
                    "OK"
                );
                return;
            }

            var avatarRoot = avatarDescriptor.gameObject;

            var clothingTransforms = new HashSet<Transform>(
                clothing.GetComponentsInChildren<Transform>(true)
            );

            var avatarBones = new Dictionary<string, Transform>();
            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == avatarRoot.transform)
                    continue;
                if (clothingTransforms.Contains(t))
                    continue;
                if (!avatarBones.ContainsKey(t.name))
                    avatarBones[t.name] = t;
            }

            int syncedCount = 0;
            Undo.SetCurrentGroupName("Sync Clothing Bone Transforms");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var t in clothing.GetComponentsInChildren<Transform>(true))
            {
                if (t == clothing.transform)
                    continue;
                if (!avatarBones.TryGetValue(t.name, out var avatarBone))
                    continue;

                Undo.RecordObject(t, "Sync Clothing Bone Transforms");
                t.localPosition = avatarBone.localPosition;
                t.localScale = avatarBone.localScale;

                var sourceScaleAdjuster = avatarBone.GetComponent<ModularAvatarScaleAdjuster>();
                if (sourceScaleAdjuster != null)
                {
                    var destScaleAdjuster = t.GetComponent<ModularAvatarScaleAdjuster>();
                    if (destScaleAdjuster == null)
                        destScaleAdjuster = Undo.AddComponent<ModularAvatarScaleAdjuster>(
                            t.gameObject
                        );
                    else
                        Undo.RecordObject(destScaleAdjuster, "Sync Clothing Bone Transforms");

                    destScaleAdjuster.Scale = sourceScaleAdjuster.Scale;
                }

                syncedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[SyncClothingBoneTransforms] {syncedCount} ボーンを同期しました。 (Avatar: {avatarRoot.name})"
            );
        }

        [MenuItem("GameObject/Sync Clothing Bone Transforms from Avatar", true)]
        public static bool ValidateSyncBoneTransforms()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
                return false;

            var descriptor = selected.GetComponentInParent<VRCAvatarDescriptor>();
            if (descriptor == null)
                return false;

            // 選択オブジェクト自体がアバタールートでないことを確認
            return descriptor.gameObject != selected;
        }
    }
}
