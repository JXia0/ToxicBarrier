using UnityEngine;
#if UNITY_EDITOR || !UNITY_2022_2_OR_NEWER
// Cinemachine 2.x 随 Unity 2022.2+ 提供，若未安装包则此脚本不引用
#endif

namespace ToxicBarrier.Camera
{
    /// <summary>
    /// 在运行时将 Cinemachine 虚拟相机的 Follow 目标设为玩家。
    /// 挂到场景里任意 GameObject 上（或挂到 Virtual Camera 同一物体上），
    /// 若未指定 Virtual Camera / 玩家，会按 Tag "Player" 和场景中的 CinemachineVirtualCamera 自动查找。
    /// </summary>
    public class CinemachineFollowPlayer : MonoBehaviour
    {
        [Header("绑定（可选，留空则自动查找）")]
        [SerializeField] private UnityEngine.Camera mainCamera;
        [SerializeField] private Transform followTarget;
        [Tooltip("Cinemachine 2D 虚拟相机。留空则从场景中取第一个。")]
        [SerializeField] private Component virtualCameraComponent;

        [Header("查找设置")]
        [SerializeField] private string playerTag = "Player";

        [Header("视角（缩进/拉远）")]
        [Tooltip("正交相机显示范围。数值越小画面越近（缩进），越大视野越远。0 表示不修改，使用相机当前设置。")]
        [SerializeField] private float orthographicSize = 0f;

        private void Awake()
        {
            if (followTarget == null)
            {
                var player = GameObject.FindWithTag(playerTag);
                if (player != null)
                    followTarget = player.transform;
                else
                    Debug.LogWarning("[CinemachineFollowPlayer] 未指定 Follow 目标且场景中无 Tag 为 \"" + playerTag + "\" 的对象。");
            }

            if (followTarget == null) return;

            // 尝试通过反射设置 Cinemachine Virtual Camera，避免硬引用导致未装包时报错
            var vcam = GetVirtualCamera();
            if (vcam != null)
            {
                SetFollow(vcam, followTarget);
                if (orthographicSize > 0f)
                    SetOrthographicSize(vcam, orthographicSize);
            }
            else
                Debug.LogWarning("[CinemachineFollowPlayer] 未找到 Cinemachine Virtual Camera。请确认已安装 Cinemachine 并在场景中创建 Virtual Camera。");

            // 若未使用 VCam 或 VCam 不控制 Lens，可直接改主相机
            if (orthographicSize > 0f)
            {
                var cam = mainCamera != null ? mainCamera : UnityEngine.Camera.main;
                if (cam != null && cam.orthographic)
                    cam.orthographicSize = orthographicSize;
            }
        }

        private Component GetVirtualCamera()
        {
            if (virtualCameraComponent != null) return virtualCameraComponent;

            // 查找场景中 CinemachineVirtualCamera 类型
            var type = GetCinemachineVirtualCameraType();
            if (type == null) return null;

            var vcam = UnityEngine.Object.FindObjectOfType(type) as Component;
            return vcam;
        }

        private static System.Type GetCinemachineVirtualCameraType()
        {
            var assembly = System.Reflection.Assembly.Load("Unity.Cinemachine");
            if (assembly == null) return null;
            return assembly.GetType("Unity.Cinemachine.CinemachineVirtualCamera");
        }

        private static void SetFollow(Component vcam, Transform target)
        {
            var type = vcam.GetType();
            var prop = type.GetProperty("Follow", typeof(Transform));
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(vcam, target);
                return;
            }
            var field = type.GetField("Follow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(Transform))
            {
                field.SetValue(vcam, target);
            }
        }

        private static void SetOrthographicSize(Component vcam, float size)
        {
            var type = vcam.GetType();
            var lensProp = type.GetProperty("Lens");
            if (lensProp == null || !lensProp.CanRead || !lensProp.CanWrite) return;
            object lens = lensProp.GetValue(vcam);
            if (lens == null) return;
            var lensType = lens.GetType();
            var sizeField = lensType.GetField("OrthographicSize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (sizeField != null && sizeField.FieldType == typeof(float))
            {
                sizeField.SetValue(lens, size);
                lensProp.SetValue(vcam, lens);
            }
        }
    }
}
