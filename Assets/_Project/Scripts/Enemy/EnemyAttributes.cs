using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 敌人基础数据 (HP + Speed)。
    /// 行为差异(Fast 闪避 / Tank 护盾爆炸)通过组件挂载实现,不在此处配置。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyStats", menuName = "Enemy/Enemy Attribute")]
    public class EnemyAttributes : ScriptableObject
    {
        [Header("基础属性")]
        [Tooltip("最大生命值(波次倍率作用前的基础值)")]
        public float baseHealth = 100f;

        [Tooltip("移动速度(波次倍率作用前的兼容字段；ECS Flocking 使用 EnemyFlockSettings)")]
        public float baseSpeed = 3f;

        [Tooltip("敌人类型(用于 ILockable 分类)")]
        public EnemyType enemyType = EnemyType.Normal;

        [Header("Legacy Flocking Data")]
        [Tooltip("旧 Mono Flocking 的兼容数据；运行时不再读取")]
        public float flockNeighbourDistance = 5f;

        [Tooltip("旧 Mono Flocking 的兼容数据；运行时不再读取")]
        public float flockSeparationDistance = 2f;

        [Tooltip("旧 Mono Flocking 的兼容数据；运行时不再读取")]
        public float flockRotationSpeed = 5f;
    }
}
