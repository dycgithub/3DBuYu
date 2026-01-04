using UnityEngine;

namespace SphereMovement
{
    /// <summary>
    /// 球面移动控制器
    /// 负责在球面上移动物体，处理输入和位置更新
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
        [Tooltip("移动速度")]
        public float moveSpeed = 5f;

        [Tooltip("是否使用平滑移动")]
        public bool useSmoothMovement = true;

        [Tooltip("平滑时间")]
        public float smoothTime = 0.1f;

        // 球坐标状态
        private Vector2 _currentSphericalCoords;
        private Vector2 _targetSphericalCoords;
        private Vector2 _velocity;

        // 缓存
        private Transform _cachedTransform;
        private Transform _targetObjectTransform;

        // 属性
        public float CurrentLongitude => _currentSphericalCoords.x;
        public float CurrentLatitude => _currentSphericalCoords.y;
        public Vector3 CurrentPositionOnSphere { get; private set; }

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

            CurrentPositionOnSphere = relativePos.normalized * sphereRadius + sphereCenter;
            _currentSphericalCoords = SphericalCoordinates.FromCartesian(relativePos);
            _targetSphericalCoords = _currentSphericalCoords;
        }

        private void Update()
        {
            ProcessInput();
            UpdateMovement();
        }

        private void ProcessInput()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            if (horizontal == 0f && vertical == 0f) return;

            float longitudeDelta = horizontal * moveSpeed * Time.deltaTime;
            float latitudeDelta = vertical * moveSpeed * Time.deltaTime;

            _targetSphericalCoords.x += longitudeDelta;
            _targetSphericalCoords.y = Mathf.Clamp(
                _targetSphericalCoords.y + latitudeDelta,
                -Mathf.PI / 2f + 0.01f,
                Mathf.PI / 2f - 0.01f
            );
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

            // 球坐标转笛卡尔坐标
            Vector3 relativePos = SphericalCoordinates.ToCartesian(_currentSphericalCoords);
            CurrentPositionOnSphere = sphereCenter + relativePos;

            // 更新位置和朝向
            ApplyPositionAndRotation(target);
        }

        private void ApplyPositionAndRotation(Transform target)
        {
            target.position = CurrentPositionOnSphere;

            Vector3 forwardDir = CalculateMovementDirection();
            if (forwardDir.sqrMagnitude > 0.001f)
            {
                target.rotation = Quaternion.LookRotation(forwardDir);
            }
        }

        private Vector3 CalculateMovementDirection()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 longitudeTangent = GetLongitudeTangent();
            Vector3 latitudeTangent = GetLatitudeTangent();

            return (longitudeTangent * horizontal + latitudeTangent * vertical).normalized;
        }

        private Vector3 GetLongitudeTangent()
        {
            Vector3 pos = TargetObject.position - sphereCenter;
            return Vector3.Cross(pos, Vector3.up).normalized;
        }

        private Vector3 GetLatitudeTangent()
        {
            Vector3 pos = TargetObject.position - sphereCenter;
            return Vector3.Cross(Vector3.up, pos).normalized;
        }

        public void SetPositionOnSphere(float longitude, float latitude)
        {
            _targetSphericalCoords.x = longitude;
            _targetSphericalCoords.y = Mathf.Clamp(
                latitude,
                -Mathf.PI / 2f + 0.01f,
                Mathf.PI / 2f - 0.01f
            );
        }

        // 供编辑器脚本使用的公开方法
        public Vector3 GetSphereCenter() => sphereCenter;
        public float GetSphereRadius() => sphereRadius;
        public Vector2 GetCurrentSphericalCoords() => _currentSphericalCoords;
    }
}
