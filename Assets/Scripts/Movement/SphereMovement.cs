using UnityEngine;

namespace SphereMovement
{
    /// <summary>
    /// 球面移动控制器
    /// 负责在球面上移动物体，处理输入和位置更新
    /// 特点：
    /// - 物体始终朝向球心
    /// - 左右沿纬线移动，上下沿经线移动
    /// - 自动处理极地方向变化
    /// </summary>
    [RequireComponent(typeof(SphereMovementGizmos))]
    public class SphereMovement : MonoBehaviour
    {
        [Header("目标设置")]
        [Tooltip("要在球面上移动的物体。如果为空，则使用挂载此脚本的物体。")]
        public Transform targetObject;

        [Header("球体设置")]
        [Tooltip("球心位置")]
        public Vector3 sphereCenter = Vector3.zero;

        [Tooltip("球半径")]
        [Range(1f, 100f)]
        public float sphereRadius = 5f;

        [Header("移动设置")]
        [Tooltip("移动速度 (度/秒)")]
        public float moveSpeed = 30f;

        [Tooltip("是否使用平滑移动")]
        public bool useSmoothMovement = true;

        [Tooltip("平滑时间")]
        public float smoothTime = 0.1f;

        // 球坐标状态 (x=经度, y=纬度，单位：弧度)
        private Vector2 _currentSphericalCoords;
        private Vector2 _targetSphericalCoords;
        private Vector2 _velocity;

        // 缓存
        private Transform _cachedTransform;
        private Transform _targetObjectTransform;

        // 上一帧的朝向向量，用于检测极点
        private Vector3 _lastForwardDirection;

        // 属性
        public float CurrentLongitude => _currentSphericalCoords.x;
        public float CurrentLatitude => _currentSphericalCoords.y;
        public Vector3 CurrentPositionOnSphere { get; private set; }
        public Vector3 CurrentPositionNormalized { get; private set; }

        public Transform TargetObject => _targetObjectTransform ?? _cachedTransform;

        private void Awake()
        {
            _cachedTransform = transform;
            _targetObjectTransform = targetObject;
            InitializeSphericalPosition();
        }

        private void OnValidate()
        {
            if (Application.isPlaying == false && targetObject != null)
            {
                _targetObjectTransform = targetObject;
            }
        }

        private void InitializeSphericalPosition()
        {
            Transform target = TargetObject;
            if (target == null) return;

            Vector3 relativePos = target.position - sphereCenter;
            if (relativePos.sqrMagnitude < 0.0001f)
            {
                relativePos = Vector3.up;
            }

            Vector3 normalizedPos = relativePos.normalized;
            CurrentPositionNormalized = normalizedPos;
            CurrentPositionOnSphere = sphereCenter + normalizedPos * sphereRadius;
            _currentSphericalCoords = SphericalCoordinates.FromCartesian(normalizedPos);
            _targetSphericalCoords = _currentSphericalCoords;
        }

        private void Update()
        {
            ProcessInput();
            UpdateMovement();
        }

        private void ProcessInput()
        {
            float horizontal = Input.GetAxis("Horizontal"); // 左右 - 沿纬线 (改变经度)
            float vertical = Input.GetAxis("Vertical");     // 上下 - 沿经线 (改变纬度)

            if (horizontal == 0f && vertical == 0f) return;

            // 将角度转换为弧度
            float longitudeDelta = horizontal * moveSpeed * Time.deltaTime * Mathf.Deg2Rad;
            float latitudeDelta = vertical * moveSpeed * Time.deltaTime * Mathf.Deg2Rad;

            // 经度：左右移动，沿纬线
            _targetSphericalCoords.x += longitudeDelta;

            // 纬度：上下移动，沿经线
            _targetSphericalCoords.y += latitudeDelta;

            // 限制纬度范围 (-90度 到 90度)
            float maxLat = Mathf.PI / 2f - 0.001f;
            _targetSphericalCoords.y = Mathf.Clamp(_targetSphericalCoords.y, -maxLat, maxLat);
        }

        private void UpdateMovement()
        {
            Transform target = TargetObject;
            if (target == null) return;

            // 平滑插值
            if (useSmoothMovement)
            {
                _currentSphericalCoords.x = Mathf.SmoothDamp(
                    _currentSphericalCoords.x, _targetSphericalCoords.x,
                    ref _velocity.x, smoothTime);
                _currentSphericalCoords.y = Mathf.SmoothDamp(
                    _currentSphericalCoords.y, _targetSphericalCoords.y,
                    ref _velocity.y, smoothTime);
            }
            else
            {
                _currentSphericalCoords = _targetSphericalCoords;
            }

            // 球坐标转笛卡尔坐标（归一化）
            Vector3 normalizedPos = SphericalCoordinates.ToCartesian(_currentSphericalCoords);
            CurrentPositionNormalized = normalizedPos;
            CurrentPositionOnSphere = sphereCenter + normalizedPos * sphereRadius;

            // 更新位置
            target.position = CurrentPositionOnSphere;

            // 更新朝向：物体正面朝向球心
            UpdateOrientation(target, normalizedPos);
        }

        private void UpdateOrientation(Transform target, Vector3 normalizedPos)
        {
            // 计算从物体指向球心的方向（即物体的 -forward 方向）
            Vector3 toCenter = (sphereCenter - target.position).normalized;

            // 计算物体的右方向（东西方向，沿纬线切线）
            Vector3 rightDir = GetLatitudeTangent(normalizedPos);

            // 处理极点附近的情况
            float latitudeAbs = Mathf.Abs(_currentSphericalCoords.y);
            bool nearPole = latitudeAbs > 1.4f; // 约80度

            if (nearPole)
            {
                // 在极点附近，使用上一帧的右方向进行平滑过渡
                rightDir = Vector3.Lerp(rightDir, _lastForwardDirection, Time.deltaTime * 10f);
                rightDir = rightDir.normalized;
            }

            // 计算物体的上方向（指向球心）
            Vector3 upDir = toCenter;

            // 计算前方向（右 × 上）
            Vector3 forwardDir = Vector3.Cross(rightDir, upDir).normalized;

            // 检查前方向是否有效
            if (forwardDir.sqrMagnitude < 0.01f)
            {
                // 极端情况，使用备份方法
                forwardDir = Vector3.Cross(Vector3.up, upDir).normalized;
                if (forwardDir.sqrMagnitude < 0.01f)
                {
                    forwardDir = Vector3.forward;
                }
            }

            // 存储右方向供下一帧使用
            _lastForwardDirection = rightDir;

            // 应用旋转：使用 LookRotation，使前方向指向运动方向（可选）
            // 如果需要物体始终面朝"北"，使用以下代码：
            Quaternion targetRotation = Quaternion.LookRotation(forwardDir, upDir);
            target.rotation = targetRotation;
        }

        /// <summary>
        /// 获取纬线切线方向（东西方向，用于左右移动）
        /// </summary>
        public Vector3 GetLatitudeTangent(Vector3 normalizedPos)
        {
            // 纬线方向是东西方向，垂直于经线
            // 使用叉乘：纬度切线 = 北极方向 × 物体位置
            Vector3 north = Vector3.up;
            Vector3 tangent = Vector3.Cross(north, normalizedPos).normalized;

            // 如果接近极点，叉乘结果可能很小，使用东西方向的近似
            if (tangent.sqrMagnitude < 0.01f)
            {
                tangent = Vector3.Cross(normalizedPos, Vector3.forward).normalized;
                if (tangent.sqrMagnitude < 0.01f)
                {
                    tangent = Vector3.Cross(normalizedPos, Vector3.right).normalized;
                }
            }

            return tangent;
        }

        /// <summary>
        /// 获取经线切线方向（南北方向，用于上下移动）
        /// </summary>
        public Vector3 GetLongitudeTangent(Vector3 normalizedPos)
        {
            // 经线方向是南北方向
            // 经线切线 = 物体位置 × 纬线切线 或 物体位置 × 东方向
            Vector3 east = Vector3.Cross(Vector3.up, normalizedPos).normalized;
            return Vector3.Cross(normalizedPos, east).normalized;
        }

        public void SetPositionOnSphere(float longitudeDegrees, float latitudeDegrees)
        {
            _targetSphericalCoords.x = longitudeDegrees * Mathf.Deg2Rad;
            _targetSphericalCoords.y = Mathf.Clamp(
                latitudeDegrees * Mathf.Deg2Rad,
                -Mathf.PI / 2f + 0.001f,
                Mathf.PI / 2f - 0.001f
            );
        }

        // 供编辑器脚本使用的公开方法
        public Vector3 GetSphereCenter() => sphereCenter;
        public float GetSphereRadius() => sphereRadius;
        public Vector2 GetCurrentSphericalCoords() => _currentSphericalCoords;
        public Vector3 GetCurrentNormalizedPosition() => CurrentPositionNormalized;
    }
}
