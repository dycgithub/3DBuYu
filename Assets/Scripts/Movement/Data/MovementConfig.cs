using UnityEngine;

namespace SphereMovement.Data
{
    /// <summary>
    /// 移动配置数据
    /// 集中管理所有移动相关的参数
    /// </summary>
    [CreateAssetMenu(fileName = "MovementConfig", menuName = "Movement/Config", order = 1)]
    public class MovementConfig : ScriptableObject
    {
        [Header("移动速度")]
        [Tooltip("基础移动速度")]
        [SerializeField] private float moveSpeed = 5f;

        [Tooltip("旋转速度")]
        [SerializeField] private float rotationSpeed = 10f;

        [Tooltip("奔跑速度倍率")]
        [SerializeField] private float sprintMultiplier = 1.5f;

        [Header("平滑设置")]
        [Tooltip("位置平滑时间")]
        [SerializeField] private float positionSmoothTime = 0.1f;

        [Tooltip("旋转平滑时间")]
        [SerializeField] private float rotationSmoothTime = 0.05f;

        [Tooltip("速度变化平滑时间")]
        [SerializeField] private float velocitySmoothTime = 0.05f;

        [Header("球面移动")]
        [Tooltip("球面移动速度（度/秒）")]
        [SerializeField] private float sphericalMoveSpeed = 30f;

        [Tooltip("纬度限制（度）")]
        [SerializeField] private float latitudeLimit = 89f;

        [Header("输入设置")]
        [Tooltip("输入死区")]
        [SerializeField] private float inputDeadZone = 0.001f;

        [Tooltip("是否使用Raw输入")]
        [SerializeField] private bool useRawInput = true;

        [Header("调试")]
        [Tooltip("是否显示调试信息")]
        [SerializeField] private bool showDebugInfo = false;

        #region 属性访问器

        public float MoveSpeed => moveSpeed;
        public float RotationSpeed => rotationSpeed;
        public float SprintMultiplier => sprintMultiplier;
        public float PositionSmoothTime => positionSmoothTime;
        public float RotationSmoothTime => rotationSmoothTime;
        public float VelocitySmoothTime => velocitySmoothTime;
        public float SphericalMoveSpeed => sphericalMoveSpeed;
        public float LatitudeLimit => latitudeLimit;
        public float InputDeadZone => inputDeadZone;
        public bool UseRawInput => useRawInput;
        public bool ShowDebugInfo => showDebugInfo;

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取有效的纬度限制（弧度）
        /// </summary>
        public float GetLatitudeLimitRadians()
        {
            return Mathf.Min(latitudeLimit, 89.9f) * Mathf.Deg2Rad;
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public MovementConfig Clone()
        {
            var clone = CreateInstance<MovementConfig>();
            clone.moveSpeed = this.moveSpeed;
            clone.rotationSpeed = this.rotationSpeed;
            clone.sprintMultiplier = this.sprintMultiplier;
            clone.positionSmoothTime = this.positionSmoothTime;
            clone.rotationSmoothTime = this.rotationSmoothTime;
            clone.velocitySmoothTime = this.velocitySmoothTime;
            clone.sphericalMoveSpeed = this.sphericalMoveSpeed;
            clone.latitudeLimit = this.latitudeLimit;
            clone.inputDeadZone = this.inputDeadZone;
            clone.useRawInput = this.useRawInput;
            clone.showDebugInfo = this.showDebugInfo;
            return clone;
        }

        #endregion

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.01f, moveSpeed);
            rotationSpeed = Mathf.Max(0.01f, rotationSpeed);
            sprintMultiplier = Mathf.Max(1f, sprintMultiplier);
            positionSmoothTime = Mathf.Max(0.001f, positionSmoothTime);
            rotationSmoothTime = Mathf.Max(0.001f, rotationSmoothTime);
            sphericalMoveSpeed = Mathf.Max(1f, sphericalMoveSpeed);
            latitudeLimit = Mathf.Clamp(latitudeLimit, 0f, 89.9f);
        }
    }
}
