using SphereMovement.Environment;
using SphereMovement.Input;
using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Core
{
    /// <summary>
    /// 表面移动组件
    /// 支持平面移动和球面移动两种模式
    /// </summary>
    [AddComponentMenu("Movement/Surface Movement")]
    [RequireComponent(typeof(MovementInput))]
    public class SurfaceMovement : MonoBehaviour
    {
        #region 序列化字段

        [Header("目标设置")]
        [Tooltip("要移动的物体，为空则使用当前物体")]
        [SerializeField] private Transform targetObject;

        [Header("移动模式")]
        [Tooltip("移动模式")]
        [SerializeField] private MovementMode movementMode = MovementMode.Plane;

        [Tooltip("球面环境（仅在球面模式下需要）")]
        [SerializeField] private SphereSurface sphereSurface;

        [Header("移动参数")]
        [Tooltip("移动速度")]
        [SerializeField] private float moveSpeed = 5f;

        [Tooltip("旋转速度")]
        [SerializeField] private float rotationSpeed = 10f;

        [Tooltip("是否使用平滑移动")]
        [SerializeField] private bool useSmoothMovement = true;

        [Tooltip("平滑时间")]
        [SerializeField] private float smoothTime = 0.1f;

        [Header("约束")]
        [Tooltip("是否朝向移动方向")]
        [SerializeField] private bool faceMovementDirection = true;

        [Tooltip("是否限制在范围内")]
        [SerializeField] private bool clampToBounds = false;

        [Tooltip("移动范围半径（平面模式）")]
        [SerializeField] private float moveRange = 50f;

        #endregion

        #region 私有字段

        private MovementInput _inputComponent;
        private IMovementInputHandler _inputHandler;
        private Vector3 _currentVelocity;
        private Vector3 _targetPosition;
        private Vector3 _currentSphericalCoords;
        private Vector3 _targetSphericalCoords;
        private Transform _cachedTransform;
        private bool _isInitialized;

        #endregion

        #region 公共属性

        /// <summary>
        /// 目标物体
        /// </summary>
        public Transform Target => targetObject ?? _cachedTransform;

        /// <summary>
        /// 当前移动模式
        /// </summary>
        public MovementMode Mode => movementMode;

        /// <summary>
        /// 当前速度
        /// </summary>
        public Vector3 CurrentVelocity { get; private set; }

        /// <summary>
        /// 是否正在移动
        /// </summary>
        public bool IsMoving => CurrentVelocity.sqrMagnitude > 0.01f;

        /// <summary>
        /// 输入处理器（可注入自定义实现）
        /// </summary>
        public IMovementInputHandler InputHandler
        {
            get => _inputHandler;
            set
            {
                _inputHandler = value ?? throw new System.ArgumentNullException(nameof(value));
                if (_inputHandler is SurfaceMovementInput smi)
                {
                    smi.MoveSpeed = moveSpeed;
                }
            }
        }

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            _cachedTransform = transform;
            _inputComponent = GetComponent<MovementInput>();
            Initialize();
        }

        private void Start()
        {
            if (movementMode == MovementMode.Spherical && sphereSurface == null)
            {
                Debug.LogWarning($"[{nameof(SurfaceMovement)}] 球面模式下未指定 SphereSurface，尝试查找...", this);
                sphereSurface = FindAnyObjectByType<SphereSurface>();

                if (sphereSurface == null)
                {
                    Debug.LogError($"[{nameof(SurfaceMovement)}] 无法找到 SphereSurface，切换为平面模式", this);
                    movementMode = MovementMode.Plane;
                }
            }

            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            UpdateMovement();
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            smoothTime = Mathf.Max(0.001f, smoothTime);
            moveRange = Mathf.Max(0f, moveRange);
        }

        #endregion

        #region 初始化

        private void Initialize()
        {
            _targetPosition = Target.position;

            if (movementMode == MovementMode.Spherical && sphereSurface != null)
            {
                InitializeSphericalPosition();
            }

            // 创建默认输入处理器
            if (_inputHandler == null && _inputComponent != null)
            {
                _inputHandler = new SurfaceMovementInput(_inputComponent)
                {
                    MoveSpeed = moveSpeed
                };
            }
        }

        private void InitializeSphericalPosition()
        {
            if (sphereSurface == null) return;

            Vector3 relativePos = Target.position - sphereSurface.Center;
            Vector3 normalizedPos = relativePos.normalized;

            _currentSphericalCoords = CartesianToSpherical(normalizedPos);
            _targetSphericalCoords = _currentSphericalCoords;
        }

        #endregion

        #region 移动更新

        private void UpdateMovement()
        {
            if (_inputHandler == null) return;

            Vector2 inputDelta = _inputHandler.ProcessInput(Time.deltaTime);

            if (movementMode == MovementMode.Spherical)
            {
                UpdateSphericalMovement(inputDelta);
            }
            else
            {
                UpdatePlaneMovement(inputDelta);
            }
        }

        private void UpdateSphericalMovement(Vector2 inputDelta)
        {
            if (sphereSurface == null) return;

            // 更新目标球坐标
            _targetSphericalCoords.x += inputDelta.x; // 经度
            _targetSphericalCoords.y += inputDelta.y; // 纬度
            _targetSphericalCoords.y = ClampLatitude(_targetSphericalCoords.y);

            // 平滑插值
            if (useSmoothMovement)
            {
                _currentSphericalCoords.x = Mathf.SmoothDampAngle(
                    _currentSphericalCoords.x * Mathf.Rad2Deg,
                    _targetSphericalCoords.x * Mathf.Rad2Deg,
                    ref _currentVelocity.x,
                    smoothTime
                ) * Mathf.Deg2Rad;

                _currentSphericalCoords.y = Mathf.SmoothDamp(
                    _currentSphericalCoords.y,
                    _targetSphericalCoords.y,
                    ref _currentVelocity.y,
                    smoothTime
                );
            }
            else
            {
                _currentSphericalCoords = _targetSphericalCoords;
            }

            // 计算位置
            Vector3 normalizedPos = SphericalToCartesian(_currentSphericalCoords);
            Vector3 worldPos = sphereSurface.Center + normalizedPos * sphereSurface.Radius;

            // 计算朝向
            if (faceMovementDirection && inputDelta.sqrMagnitude > 0.001f)
            {
                UpdateSphericalOrientation(normalizedPos, inputDelta);
            }
            else
            {
                // 仅保持朝向球心的反方向（站立姿态）
                Vector3 upDir = normalizedPos;
                Vector3 forwardDir = Vector3.ProjectOnPlane(Vector3.forward, upDir).normalized;
                if (forwardDir.sqrMagnitude < 0.001f)
                {
                    forwardDir = Vector3.ProjectOnPlane(Vector3.right, upDir).normalized;
                }
                Target.rotation = Quaternion.LookRotation(forwardDir, upDir);
            }

            // 更新位置
            Target.position = worldPos;

            // 更新速度记录
            CurrentVelocity = (worldPos - _targetPosition) / Time.deltaTime;
            _targetPosition = worldPos;
        }

        private void UpdatePlaneMovement(Vector2 inputDelta)
        {
            // 计算目标位置
            Vector3 moveDir = new Vector3(inputDelta.x, 0f, inputDelta.y).normalized;
            float moveDistance = inputDelta.magnitude * moveSpeed;
            Vector3 targetPos = Target.position + moveDir * moveDistance;

            // 范围限制
            if (clampToBounds)
            {
                Vector3 fromCenter = targetPos - transform.position;
                if (fromCenter.sqrMagnitude > moveRange * moveRange)
                {
                    targetPos = transform.position + fromCenter.normalized * moveRange;
                }
            }

            // 平滑移动
            if (useSmoothMovement)
            {
                Target.position = Vector3.SmoothDamp(
                    Target.position,
                    targetPos,
                    ref _currentVelocity,
                    smoothTime
                );
            }
            else
            {
                Target.position = targetPos;
            }

            // 朝向
            if (faceMovementDirection && moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
                Target.rotation = Quaternion.Slerp(
                    Target.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }

            // 更新速度
            CurrentVelocity = (Target.position - _targetPosition) / Time.deltaTime;
            _targetPosition = Target.position;
        }

        private void UpdateSphericalOrientation(Vector3 normalizedPos, Vector2 inputDelta)
        {
            // 计算球面上的移动方向
            Vector3 eastDir = Vector3.Cross(Vector3.up, normalizedPos).normalized;
            if (eastDir.sqrMagnitude < 0.001f)
            {
                eastDir = Vector3.Cross(Vector3.forward, normalizedPos).normalized;
            }

            Vector3 northDir = Vector3.Cross(normalizedPos, eastDir).normalized;

            // 移动方向
            Vector3 moveDir = (eastDir * inputDelta.x + northDir * inputDelta.y).normalized;

            // 目标朝向
            Vector3 upDir = normalizedPos;
            Quaternion targetRotation = Quaternion.LookRotation(moveDir, upDir);

            // 平滑旋转
            Target.rotation = Quaternion.Slerp(
                Target.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 笛卡尔坐标转球坐标
        /// </summary>
        private Vector3 CartesianToSpherical(Vector3 normalizedPos)
        {
            float longitude = Mathf.Atan2(normalizedPos.x, normalizedPos.z);
            float latitude = Mathf.Asin(normalizedPos.y);
            return new Vector3(longitude, latitude, 0f);
        }

        /// <summary>
        /// 球坐标转笛卡尔坐标
        /// </summary>
        private Vector3 SphericalToCartesian(Vector3 spherical)
        {
            float cosLat = Mathf.Cos(spherical.y);
            return new Vector3(
                cosLat * Mathf.Sin(spherical.x),
                Mathf.Sin(spherical.y),
                cosLat * Mathf.Cos(spherical.x)
            );
        }

        /// <summary>
        /// 限制纬度范围
        /// </summary>
        private float ClampLatitude(float latitude)
        {
            float maxLat = Mathf.PI / 2f - 0.001f;
            return Mathf.Clamp(latitude, -maxLat, maxLat);
        }

        /// <summary>
        /// 设置球面上的位置（仅球面模式）
        /// </summary>
        public void SetPositionOnSphere(float longitudeDegrees, float latitudeDegrees)
        {
            if (movementMode != MovementMode.Spherical)
            {
                Debug.LogWarning($"[{nameof(SurfaceMovement)}] 只能在球面模式下设置球面位置");
                return;
            }

            _targetSphericalCoords.x = longitudeDegrees * Mathf.Deg2Rad;
            _targetSphericalCoords.y = ClampLatitude(latitudeDegrees * Mathf.Deg2Rad);
        }

        /// <summary>
        /// 切换移动模式
        /// </summary>
        public void SetMovementMode(MovementMode mode, SphereSurface surface = null)
        {
            if (mode == MovementMode.Spherical && surface == null && sphereSurface == null)
            {
                Debug.LogError($"[{nameof(SurfaceMovement)}] 球面模式需要指定 SphereSurface");
                return;
            }

            if (surface != null)
            {
                sphereSurface = surface;
            }

            movementMode = mode;

            if (mode == MovementMode.Spherical)
            {
                InitializeSphericalPosition();
            }
        }

        #endregion
    }

    /// <summary>
    /// 移动模式
    /// </summary>
    public enum MovementMode
    {
        /// <summary>平面移动</summary>
        Plane,

        /// <summary>球面移动</summary>
        Spherical
    }
}
