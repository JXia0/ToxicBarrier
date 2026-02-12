using UnityEngine;

namespace ToxicBarrier.Camera
{
    /// <summary>
    /// 根据当前解锁的 Map 环更新相机边界：只解锁 Map 1 时相机被限制在 Map 1 内；
    /// 解锁 Map 2 后边界扩大到 Map 2；满级解锁 Map 3 后扩大到 Map 3。
    /// 使用 PolygonCollider2D 近似圆形（CinemachineConfiner2D 不支持 CircleCollider2D，只认多边形）。
    /// </summary>
    public class CameraBoundsFromMapUnlock : MonoBehaviour
    {
        [Header("绑定（可选）")]
        [Tooltip("Cinemachine Virtual Camera。留空则自动查找场景中第一个。")]
        [SerializeField] private Component virtualCameraComponent;
        [Tooltip("用于 Confiner 的边界物体（带 PolygonCollider2D）。留空则自动创建。")]
        [SerializeField] private GameObject boundsShapeObject;

        [Header("边界形状")]
        [Tooltip("近似圆形的多边形边数，越大越圆。")]
        [SerializeField] private int circleSegments = 64;

        private GameObject _boundsRoot;
        private PolygonCollider2D _boundsCollider;
        private Component _confiner;
        private float _lastRadius = -1f;
        private Vector2 _lastCenter;

        private void Start()
        {
            EnsureBoundsShape();
            EnsureConfiner();
            RefreshBounds();
        }

        private void LateUpdate()
        {
            RefreshBounds();
        }

        private void EnsureBoundsShape()
        {
            if (boundsShapeObject != null)
            {
                _boundsCollider = boundsShapeObject.GetComponent<PolygonCollider2D>();
                if (_boundsCollider != null)
                {
                    _boundsRoot = boundsShapeObject;
                    return;
                }
            }

            Transform t = transform.Find("CameraBounds");
            if (t != null)
            {
                _boundsRoot = t.gameObject;
                _boundsCollider = _boundsRoot.GetComponent<PolygonCollider2D>();
                if (_boundsCollider != null) return;
            }

            _boundsRoot = new GameObject("CameraBounds");
            _boundsRoot.transform.SetParent(transform);
            _boundsCollider = _boundsRoot.AddComponent<PolygonCollider2D>();
            _boundsCollider.isTrigger = true;
            _boundsCollider.SetPath(0, GetCirclePoints(1f));
        }

        private Vector2[] GetCirclePoints(float radius)
        {
            int n = Mathf.Max(8, circleSegments);
            var points = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float angle = (i / (float)n) * Mathf.PI * 2f;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }
            return points;
        }

        private void EnsureConfiner()
        {
            if (_confiner != null) return;

            Component vcam = virtualCameraComponent ?? GetVirtualCamera();
            if (vcam == null) return;

            var confinerType = GetCinemachineConfiner2DType();
            if (confinerType == null) return;

            _confiner = vcam.GetComponent(confinerType);
            if (_confiner == null)
                _confiner = vcam.gameObject.AddComponent(confinerType);

            SetConfinerBoundingShape(_confiner, _boundsRoot);
        }

        private static Component GetVirtualCamera()
        {
            var assembly = System.Reflection.Assembly.Load("Unity.Cinemachine");
            if (assembly == null) return null;
            var type = assembly.GetType("Unity.Cinemachine.CinemachineVirtualCamera");
            if (type == null) return null;
            return (Component)Object.FindObjectOfType(type);
        }

        private static System.Type GetCinemachineConfiner2DType()
        {
            var assembly = System.Reflection.Assembly.Load("Unity.Cinemachine");
            if (assembly == null) return null;
            return assembly.GetType("Unity.Cinemachine.CinemachineConfiner2D");
        }

        private static void SetConfinerBoundingShape(Component confiner, GameObject boundsGo)
        {
            if (confiner == null || boundsGo == null) return;
            var type = confiner.GetType();
            var prop = type.GetProperty("BoundingShape2D", typeof(Collider2D));
            if (prop != null && prop.CanWrite)
            {
                var c = boundsGo.GetComponent<Collider2D>();
                if (c != null) prop.SetValue(confiner, c);
                return;
            }
            var goProp = type.GetProperty("BoundingShape2D", typeof(GameObject));
            if (goProp != null && goProp.CanWrite)
            {
                goProp.SetValue(confiner, boundsGo);
            }
        }

        private static void InvalidateConfinerCache(Component confiner)
        {
            if (confiner == null) return;
            var type = confiner.GetType();
            var method = type.GetMethod("InvalidateBoundingShapeCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new System.Type[0], null);
            if (method == null)
                method = type.GetMethod("InvalidateCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new System.Type[0], null);
            method?.Invoke(confiner, null);
        }

        private void RefreshBounds()
        {
            if (MapController.Instance == null || _boundsRoot == null || _boundsCollider == null) return;

            Vector2 center = MapController.Instance.GetMapCenter();
            float radius = MapController.Instance.GetMaxAllowedRadius();

            _boundsRoot.transform.position = new Vector3(center.x, center.y, _boundsRoot.transform.position.z);
            _boundsCollider.SetPath(0, GetCirclePoints(radius));

            bool changed = Mathf.Abs(radius - _lastRadius) > 0.001f || (center - _lastCenter).sqrMagnitude > 0.0001f;
            _lastRadius = radius;
            _lastCenter = center;

            if (changed && _confiner != null)
                InvalidateConfinerCache(_confiner);
        }
    }
}
