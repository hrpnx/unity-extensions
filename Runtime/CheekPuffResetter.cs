using UnityEngine;
using VRC.SDKBase;

namespace Hrpnx.UnityExtensions.CheekPuffResetter
{
    /// <summary>
    /// 頬の動き（Puff / Suck）がしきい値を超えた際に指定 PhysBone をリセットするコンポーネント
    /// </summary>
    public enum MonitorMode
    {
        [InspectorName("頬を膨らませてリセット")]
        PuffOnly,
        [InspectorName("頬をへこませてリセット")]
        SuckOnly,
    }

    /// <summary>
    /// CheekPuff / CheekSuck パラメータが閾値を超えた際に指定 PhysBone をリセットするコンポーネント
    /// </summary>
    public class CheekPuffResetter : MonoBehaviour, IEditorOnly
    {
        [Tooltip("監視するパラメータの種類")]
        public MonitorMode Mode = MonitorMode.PuffOnly;

        [Tooltip("閾値 (0-1、0.5 = 50%)")]
        [Range(0f, 1f)]
        public float Threshold = 0.5f;

        [Tooltip("リセット対象の PhysBone が付いた Transform (Cheek1_L)")]
        public Transform CheekBoneL;

        [Tooltip("リセット対象の PhysBone が付いた Transform (Cheek1_R)")]
        public Transform CheekBoneR;
    }
}
