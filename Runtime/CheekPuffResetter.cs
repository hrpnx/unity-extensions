using UnityEngine;
using VRC.SDKBase;

namespace Hrpnx.UnityExtensions.CheekPuffResetter
{
    /// <summary>
    /// CheekPuffLeft/Right パラメータが閾値を超えた際に指定 PhysBone をリセットするコンポーネント
    /// </summary>
    public class CheekPuffResetter : MonoBehaviour, IEditorOnly
    {
        [Tooltip("閾値 (0-1、0.5 = 50%)")]
        [Range(0f, 1f)]
        public float Threshold = 0.5f;

        [Tooltip("リセット対象の PhysBone が付いた Transform (Cheek1_L)")]
        public Transform CheekBoneL;

        [Tooltip("リセット対象の PhysBone が付いた Transform (Cheek1_R)")]
        public Transform CheekBoneR;
    }
}
