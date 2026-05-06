using UnityEngine;

namespace SphereMovement.Camera
{
    /// <summary>
    /// 第三人称摄像机控制器
    /// 支持球面和平面两种跟随模式
    /// </summary>
    [AddComponentMenu("Movement/Third Person Camera")]
    public class ThirdPersonCamera : MonoBehaviour
    {
        #region 序列化字段

        [Header("目标设置")]
        [Tooltip("跟随的目标")]
        [SerializeField] private Transform target;

        [Tooltip("目标偏移（相对于目标本地坐标）")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

        [Header("距离设置")]
        [Tooltip("默认距离")]
        [SerializeField] private float defaultDistance = 5f;

        [Tooltip("最小距离")]
        [SerializeField] private float minDistance = 1f;

        [Tooltip("最大距离")]
        [SerializeField] private float maxDistance = 15f;

        [Tooltip("距离调整速度")]
        [SerializeField] private float distanceSmoothSpeed = 10f;

        [Header("旋转设置")]
        [Tooltip("水平旋转速度（度/秒）")]
        [SerializeField] private float horizontalRotationSpeed = 120f;

        [Tooltip("垂直旋转速度（度/秒）")]
        [SerializeField] private float verticalRotationSpeed = 80f;

        [Tooltip("垂直角度限制（最小）")]
        [SerializeField] private float minVerticalAngle = -60f;

        [Tooltip("垂直角度限制（最大）")]
        [SerializeField] private float maxVerticalAngle = 80f;

        [Tooltip("是否自动跟随目标旋转")]
        [SerializeField] private bool followTargetRotation = false;

        [Tooltip("跟随旋转的平滑度")]
        [SerializeField] private float followRotationSmoothness = 5f;

        [Header("跟随模式")]
        [Tooltip("跟随模式")]
        [SerializeField] private CameraFollowMode followMode = CameraFollowMode.Standard;

        [Tooltip("球面模式的旋转基准")]
        [SerializeField] private Transform sphericalReference;

        [Header("平滑设置")]
        [Tooltip("是否使用平滑移动")]
        [SerializeField] private bool useSmoothMovement = true;

        [Tooltip("位置平滑时间")]
        [SerializeField] private float positionSmoothTime = 0.1f;

        [Tooltip("旋转平滑时间")]
        [SerializeField] private float rotationSmoothTime = 0.05f;

        [Header("碰撞检测")]
        [Tooltip("是否启用碰撞检测")]
        [SerializeField] private bool enableCollision = true;

        [Tooltip("碰撞层")]
        [SerializeField] private LayerMask collisionLayers = ~0;

        [Tooltip("摄像机碰撞半径")]
        [SerializeField] private float cameraRadius = 0.3f;

        #endregion

        #region 私有字段

        private float _currentDistance;
        private float _targetDistance;
        private float _currentHorizontalAngle;
        private float _currentVerticalAngle;
        private Vector3 _currentPosition;
        private Quaternion _currentRotation;
        private Vector3 _positionVelocity;
        private float _rotationVelocity;

        #endregion

        #region 公共属性

        /// <summary>
        /// 跟随目标
        /// </summary>
        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                if (target != null)
                {
                    InitializePosition();
                }
            }
        }

        /// <summary>
        /// 当前距离
        /// </summary>
        public float CurrentDistance => _currentDistance;

        /// <summary>
        /// 当前水平角度
        /// </summary>
        public float CurrentHorizontalAngle => _currentHorizontalAngle;

        /// <summary>
        /// 当前垂直角度
        /// </summary>
        public float CurrentVerticalAngle => _currentVerticalAngle;

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            _currentDistance = defaultDistance;
            _targetDistance = defaultDistance;
            _currentPosition = transform.position;
            _currentRotation = transform.rotation;
        }

        private void Start()
        {
            if (target == null)
            {
                Debug.LogWarning($"[{nameof(ThirdPersonCamera)}] 未设置跟随目标，尝试查找带有 Player 标签的对象");
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }

            if (sphericalReference == null && followMode == CameraFollowMode.Spherical)
            {
                sphericalReference = target;
            }

            InitializePosition();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleInput();
            UpdateDistance();
            UpdateRotation();
            UpdatePosition();
        }

        private void OnValidate()
        {
            defaultDistance = Mathf.Clamp(defaultDistance, minDistance, maxDistance);
            minVerticalAngle = Mathf.Clamp(minVerticalAngle, -89f, 89f);
            maxVerticalAngle = Mathf.Clamp(maxVerticalAngle, -89f, 89f);
        }

        #endregion

        #region 初始化

        private void InitializePosition()
        {
            if (target == null) return;

            // 计算初始角度
            Vector3 offset = transform.position - GetTargetPosition();
            _currentDistance = offset.magnitude;
            _targetDistance = _currentDistance;

            Vector3 direction = offset.normalized;
            _currentVerticalAngle = Mathf.Asin(direction.y) * Mathf.Rad2Deg;
            _currentHorizontalAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            _currentPosition = transform.position;
            _currentRotation = transform.rotation;
        }

        #endregion

        #region 输入处理

        private void HandleInput()
        {
            // 鼠标旋转（按住右键）
            if (UnityEngine.Input.GetMouseButton(1))
            {
                float mouseX = UnityEngine.Input.GetAxis("Mouse X");
                float mouseY = UnityEngine.Input.GetAxis("Mouse Y");

                _currentHorizontalAngle += mouseX * horizontalRotationSpeed * 0.1f;
                _currentVerticalAngle -= mouseY * verticalRotationSpeed * 0.1f;
            }

            // 滚轮缩放
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                _targetDistance -= scroll * 5f;
                _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
            }

            // 限制垂直角度
            _currentVerticalAngle = Mathf.Clamp(_currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
        }

        #endregion

        #region 更新逻辑

        private void UpdateDistance()
        {
            _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Time.deltaTime * distanceSmoothSpeed);
        }

        private void UpdateRotation()
        {
            if (followMode == CameraFollowMode.Spherical && sphericalReference != null)
            {
                // 球面模式下，旋转相对于球面参考
                UpdateSphericalRotation();
            }
            else if (followTargetRotation)
            {
                // 跟随目标旋转
                UpdateTargetFollowingRotation();
            }
        }

        private void UpdateSphericalRotation()
        {
            // 基于球面参考的旋转
            Quaternion referenceRotation = sphericalReference.rotation;
            Vector3 referenceUp = referenceRotation * Vector3.up;

            // 应用角度偏移
            Quaternion horizontalRot = Quaternion.AngleAxis(_currentHorizontalAngle, referenceUp);
            Vector3 rightDir = horizontalRot * referenceRotation * Vector3.right;
            Quaternion verticalRot = Quaternion.AngleAxis(_currentVerticalAngle, rightDir);

            _currentRotation = verticalRot * horizontalRot * referenceRotation;
        }

        private void UpdateTargetFollowingRotation()
        {
            Quaternion targetRot = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            Quaternion offsetRot = Quaternion.Euler(_currentVerticalAngle, _currentHorizontalAngle, 0f);

            _currentRotation = targetRot * offsetRot;
        }

        private void UpdatePosition()
        {
            Vector3 targetPos = GetTargetPosition();
            Vector3 desiredPosition;

            if (followMode == CameraFollowMode.Spherical && sphericalReference != null)
            {
                // 球面模式：位置基于球面参考
                desiredPosition = CalculateSphericalCameraPosition(targetPos);
            }
            else
            {
                // 标准模式：基于欧拉角的偏移
                Vector3 offset = _currentRotation * Vector3.back * _currentDistance;
                desiredPosition = targetPos + offset;
            }

            // 碰撞检测
            if (enableCollision)
            {
                desiredPosition = HandleCollision(targetPos, desiredPosition);
            }

            // 平滑移动
            if (useSmoothMovement)
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desiredPosition,
                    ref _positionVelocity,
                    positionSmoothTime
                );
            }
            else
            {
                transform.position = desiredPosition;
            }

            // 更新旋转（看向目标）
            if (followMode != CameraFollowMode.Spherical)
            {
                Quaternion lookRot = Quaternion.LookRotation(targetPos - transform.position);
                if (useSmoothMovement)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime / rotationSmoothTime);
                }
                else
                {
                    transform.rotation = lookRot;
                }
            }
            else
            {
                transform.rotation = _currentRotation;
            }
        }

        private Vector3 CalculateSphericalCameraPosition(Vector3 targetPos)
        {
            if (sphericalReference == null) return targetPos;

            // 基于球面参考的旋转
            Quaternion referenceRot = sphericalReference.rotation;

            // 应用角度偏移
            Quaternion horizontalRot = Quaternion.AngleAxis(_currentHorizontalAngle, referenceRot * Vector3.up);
            Vector3 rightDir = horizontalRot * referenceRot * Vector3.right;
            Quaternion verticalRot = Quaternion.AngleAxis(_currentVerticalAngle, rightDir);

            _currentRotation = verticalRot * horizontalRot * referenceRot;

            return targetPos + _currentRotation * Vector3.back * _currentDistance;
        }

        private Vector3 HandleCollision(Vector3 targetPos, Vector3 desiredPos)
        {
            Vector3 direction = desiredPos - targetPos;
            float distance = direction.magnitude;

            if (Physics.SphereCast(
                targetPos,
                cameraRadius,
                direction.normalized,
                out RaycastHit hit,
                distance,
                collisionLayers))
            {
                return targetPos + direction.normalized * Mathf.Max(0f, hit.distance - cameraRadius);
            }

            return desiredPos;
        }

        private Vector3 GetTargetPosition()
        {
            if (target == null) return transform.position;
            return target.position + target.rotation * targetOffset;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置目标距离
        /// </summary>
        public void SetDistance(float distance)
        {
            _targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        /// <summary>
        /// 设置旋转角度
        /// </summary>
        public void SetRotation(float horizontalAngle, float verticalAngle)
        {
            _currentHorizontalAngle = horizontalAngle;
            _currentVerticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
        }

        /// <summary>
        /// 切换到指定跟随模式
        /// </summary>
        public void SetFollowMode(CameraFollowMode mode, Transform reference = null)
        {
            followMode = mode;
            if (reference != null)
            {
                sphericalReference = reference;
            }
        }

        /// <summary>
        /// 立即重置到目标位置（无平滑过渡）
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            InitializePosition();
            transform.position = _currentPosition;
            transform.rotation = _currentRotation;
        }

        #endregion
    }

    /// <summary>
    /// 摄像机跟随模式
    /// </summary>
    public enum CameraFollowMode
    {
        /// <summary>标准第三人称跟随</summary>
        Standard,

        /// <summary>球面模式（基于球面参考的旋转）</summary>
        Spherical
    }
}
