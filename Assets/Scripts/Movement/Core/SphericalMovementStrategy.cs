using SphereMovement.Data;
using SphereMovement.Environment;
using UnityEngine;

namespace SphereMovement.Core
{
    /// <summary>
    /// 球面移动策略
    /// 在球面上移动物体
    /// </summary>
    public class SphericalMovementStrategy : MovementStrategyBase
    {
        private readonly SphereSurface _surface;
        private Vector3 _currentSphericalCoords;
        private Vector3 _targetSphericalCoords;
        private Vector2 _smoothVelocity;
        private Quaternion _currentRotation;

        public SphericalMovementStrategy(MovementConfig config, SphereSurface surface) : base(config)
        {
            _surface = surface ?? throw new System.ArgumentNullException(nameof(surface));
        }

        public override void Move(Transform target, Vector2 input, float deltaTime)
        {
            if (input.sqrMagnitude < Config.InputDeadZone)
            {
                return;
            }

            // 将输入转换为球坐标变化
            float longitudeDelta = input.x * Config.SphericalMoveSpeed * deltaTime * Mathf.Deg2Rad;
            float latitudeDelta = input.y * Config.SphericalMoveSpeed * deltaTime * Mathf.Deg2Rad;

            // 更新目标球坐标
            _targetSphericalCoords.x += longitudeDelta;
            _targetSphericalCoords.y += latitudeDelta;

            // 限制纬度
            float latLimit = Config.GetLatitudeLimitRadians();
            _targetSphericalCoords.y = Mathf.Clamp(_targetSphericalCoords.y, -latLimit, latLimit);

            // 平滑插值
            if (Config.PositionSmoothTime > 0.001f)
            {
                _currentSphericalCoords.x = SmoothDampAngle(
                    _currentSphericalCoords.x,
                    _targetSphericalCoords.x,
                    ref _smoothVelocity.x,
                    Config.PositionSmoothTime
                );

                _currentSphericalCoords.y = Mathf.SmoothDamp(
                    _currentSphericalCoords.y,
                    _targetSphericalCoords.y,
                    ref _smoothVelocity.y,
                    Config.PositionSmoothTime
                );
            }
            else
            {
                _currentSphericalCoords = _targetSphericalCoords;
            }

            // 计算世界位置
            Vector3 worldPos = SphericalToWorld(_currentSphericalCoords);

            // 更新位置
            target.position = worldPos;

            // 更新朝向
            if (Config.RotationSpeed > 0f)
            {
                UpdateRotation(target, input, deltaTime);
            }
        }

        /// <summary>
        /// 初始化球坐标
        /// </summary>
        public override void InitializeFromPosition(Vector3 worldPosition)
        {
            Vector3 localPos = worldPosition - _surface.Center;
            float radius = localPos.magnitude;

            if (radius < 0.001f)
            {
                localPos = Vector3.up;
                radius = 1f;
            }

            Vector3 normalized = localPos / radius;

            _currentSphericalCoords.x = Mathf.Atan2(normalized.x, normalized.z);
            _currentSphericalCoords.y = Mathf.Asin(normalized.y);
            _targetSphericalCoords = _currentSphericalCoords;
        }

        /// <summary>
        /// 获取当前速度（世界坐标系）
        /// </summary>
        public override Vector3 GetCurrentVelocity()
        {
            // 将球坐标速度转换为世界坐标速度
            float longitudeVel = _smoothVelocity.x; // 经度变化率
            float latitudeVel = _smoothVelocity.y;  // 纬度变化率

            // 简化处理：返回速度大小
            float speed = new Vector2(longitudeVel, latitudeVel).magnitude * _surface.Radius;
            return Vector3.forward * speed;
        }

        /// <summary>
        /// 是否可以在当前位置停止（球面移动总是可以停止）
        /// </summary>
        public override bool CanStopAtCurrentPosition()
        {
            return true;
        }

        /// <summary>
        /// 球坐标转世界坐标
        /// </summary>
        private Vector3 SphericalToWorld(Vector3 spherical)
        {
            float longitude = spherical.x;
            float latitude = spherical.y;

            float cosLat = Mathf.Cos(latitude);
            float x = cosLat * Mathf.Sin(longitude);
            float y = Mathf.Sin(latitude);
            float z = cosLat * Mathf.Cos(longitude);

            return _surface.Center + new Vector3(x, y, z) * _surface.Radius;
        }

        /// <summary>
        /// 平滑角度插值
        /// </summary>
        private float SmoothDampAngle(float current, float target, ref float velocity, float smoothTime)
        {
            float delta = Mathf.DeltaAngle(current * Mathf.Rad2Deg, target * Mathf.Rad2Deg);
            float result = Mathf.SmoothDamp(0f, delta, ref velocity, smoothTime);
            return (current * Mathf.Rad2Deg + result) * Mathf.Deg2Rad;
        }

        /// <summary>
        /// 更新旋转
        /// </summary>
        private void UpdateRotation(Transform target, Vector2 input, float deltaTime)
        {
            // 计算球面上的切线方向
            Vector3 position = target.position;
            Vector3 toCenter = (_surface.Center - position).normalized;

            // 东向切线
            Vector3 east = Vector3.Cross(Vector3.up, toCenter).normalized;
            if (east.sqrMagnitude < 0.001f)
            {
                east = Vector3.Cross(Vector3.forward, toCenter).normalized;
            }

            // 北向切线
            Vector3 north = Vector3.Cross(toCenter, east).normalized;

            // 移动方向
            Vector3 moveDir = (east * input.x + north * input.y).normalized;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, toCenter);
                target.rotation = Quaternion.Slerp(
                    target.rotation,
                    targetRot,
                    Config.RotationSpeed * deltaTime
                );
            }
        }
    }
}
