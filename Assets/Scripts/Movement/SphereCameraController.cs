using UnityEngine;

namespace SphereMovement
{
    /// <summary>
    /// 球面相机控制器
    /// 相机始终跟随球面上的物体，并保持看向球心
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SphereCameraController : MonoBehaviour
    {
        [Header("目标设置")]
        [Tooltip("球面移动控制器")]
        public SphereMovement sphereMovement;

        [Tooltip("跟随的目标物体")]
        public Transform targetObject;

        [Header("相机设置")]
        [Tooltip("相机到球心的距离")]
        public float distanceFromCenter = 10f;

        [Tooltip("相机高度偏移（相对于物体位置）")]
        public float heightOffset = 2f;

        [Tooltip("相机在物体后方的距离")]
        public float behindDistance = 3f;

        [Tooltip("看向球心时的上方向")]
        public Vector3 upReference = Vector3.up;

        [Tooltip("是否使用平滑跟随")]
        public bool useSmoothFollow = true;

        [Tooltip("平滑时间")]
        public float smoothTime = 0.1f;

        [Tooltip("是否在编辑器中预览")]
        public bool previewInEditor = false;

        // 内部状态
        private Transform _cachedTransform;
        private Transform _targetTransform;
        private Vector3 _currentVelocity;
        private Vector3 _targetPosition;
        private bool _isInitialized;

        // 上一帧的相机位置，用于检测翻转
        private Vector3 _lastCameraPosition;
        private Quaternion _lastCameraRotation;

        private void Awake()
        {
            _cachedTransform = transform;
            _currentVelocity = Vector3.zero;

            // 自动查找 SphereMovement 组件
            if (sphereMovement == null)
            {
                sphereMovement = FindFirstObjectByType<SphereMovement>();
            }
        }

        private void Start()
        {
            if (sphereMovement != null)
            {
                targetObject = sphereMovement.TargetObject;
            }

            if (targetObject == null)
            {
                Debug.LogWarning("SphereCameraController: 未设置目标物体");
                return;
            }

            _targetTransform = targetObject;
            _isInitialized = true;

            // 初始化相机位置
            InitializeCameraPosition();

            _lastCameraPosition = _cachedTransform.position;
            _lastCameraRotation = _cachedTransform.rotation;
        }

        private void LateUpdate()
        {
            if (!IsValid()) return;

            UpdateCameraPosition();
            UpdateCameraRotation();
        }

        private bool IsValid()
        {
            if (_targetTransform == null && sphereMovement != null)
            {
                _targetTransform = sphereMovement.TargetObject;
            }
            return _targetTransform != null && _isInitialized;
        }

        private void InitializeCameraPosition()
        {
            Vector3 sphereCenter = sphereMovement.GetSphereCenter();
            Vector3 objectPos = _targetTransform.position;
            Vector3 toObject = (objectPos - sphereCenter).normalized;

            // 计算相机位置：在物体后方 distanceFromCenter 距离处
            // 相机应该在球心到物体的连线上，但在物体后面
            Vector3 cameraDir = -toObject; // 从球心向外

            // 计算目标位置
            Vector3 idealPosition = sphereCenter + cameraDir * distanceFromCenter;
            idealPosition += Vector3.up * heightOffset;

            _targetPosition = idealPosition;
            _cachedTransform.position = idealPosition;

            UpdateCameraRotation();
        }

        private void UpdateCameraPosition()
        {
            Vector3 sphereCenter = sphereMovement.GetSphereCenter();
            Vector3 objectPos = _targetTransform.position;
            Vector3 toObject = (objectPos - sphereCenter).normalized;

            // 计算相机应该在的位置：
            // 在物体和球心的连线上，但在物体后方 behindDistance 距离处
            // 然后向外扩展到 distanceFromCenter
            Vector3 behindPos = objectPos + (-toObject) * behindDistance;
            Vector3 cameraDir = (behindPos - sphereCenter).normalized;

            Vector3 idealPosition = sphereCenter + cameraDir * distanceFromCenter;
            idealPosition += Vector3.up * heightOffset;

            if (useSmoothFollow)
            {
                _targetPosition = idealPosition;
                _cachedTransform.position = Vector3.SmoothDamp(
                    _cachedTransform.position,
                    idealPosition,
                    ref _currentVelocity,
                    smoothTime
                );
            }
            else
            {
                _cachedTransform.position = idealPosition;
            }
        }

        private void UpdateCameraRotation()
        {
            Vector3 sphereCenter = sphereMovement.GetSphereCenter();

            // 相机看向球心
            Vector3 toCenter = (sphereCenter - _cachedTransform.position).normalized;

            // 检查是否接近极点，避免万向节死锁
            float dotUp = Vector3.Dot(toCenter, upReference);
            bool nearPole = Mathf.Abs(dotUp) > 0.95f;

            Vector3 upDir = upReference;
            if (nearPole)
            {
                // 在极点附近，使用上一帧的up方向进行平滑过渡
                upDir = Vector3.Lerp(_lastCameraRotation * Vector3.up, upReference, Time.deltaTime * 5f).normalized;
                if (upDir.sqrMagnitude < 0.01f)
                {
                    upDir = Vector3.forward;
                }
            }

            // 使用 LookRotation 使相机看向球心
            Quaternion targetRotation = Quaternion.LookRotation(toCenter, upDir);
            _cachedTransform.rotation = targetRotation;

            _lastCameraPosition = _cachedTransform.position;
            _lastCameraRotation = _cachedTransform.rotation;
        }

        /// <summary>
        /// 设置相机距离
        /// </summary>
        public void SetDistance(float distance)
        {
            distanceFromCenter = Mathf.Max(1f, distance);
        }

        /// <summary>
        /// 设置高度偏移
        /// </summary>
        public void SetHeightOffset(float height)
        {
            heightOffset = height;
        }

        private void OnValidate()
        {
            // 在编辑器中预览
            if (Application.isPlaying == false && previewInEditor && sphereMovement != null)
            {
                sphereMovement = GetComponentInParent<SphereMovement>();
                if (sphereMovement != null)
                {
                    _cachedTransform = transform;
                    _targetTransform = sphereMovement.TargetObject;
                    if (_targetTransform != null)
                    {
                        InitializeCameraPosition();
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Vector3 sphereCenter = sphereMovement != null ? sphereMovement.GetSphereCenter() : Vector3.zero;
                Gizmos.DrawWireSphere(sphereCenter, 0.2f);
                Gizmos.DrawLine(_cachedTransform.position, sphereCenter);
            }
        }
    }
}
