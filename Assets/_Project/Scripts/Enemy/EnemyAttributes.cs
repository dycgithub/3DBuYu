using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 敌人基础数据 (HP + Speed)。
    /// 行为差异(Fast 闪避 / Tank 护盾爆炸)通过组件挂载实现,不在此处配置。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyStats", menuName = "Enemy/Enemy Stats")]
    public class EnemyAttributes : ScriptableObject
    {
        [Header("基础属性")]
        [Tooltip("最大生命值(波次倍率作用前的基础值)")]
        public float baseHealth = 100f;

        [Tooltip("移动速度(波次倍率作用前的基础值,作用于 FlockAgent)")]
        public float baseSpeed = 3f;

        [Tooltip("敌人类型(用于 ILockable 分类与积分)")]
        public EnemyType enemyType = EnemyType.Normal;

        [Tooltip("击杀提供的积分(写死于此处,不再走配置)")]
        public int pointsValue = 30;

        [Header("Flocking")]
        [Tooltip("FlockAgent 邻居检测半径")]
        public float flockNeighbourDistance = 5f;

        [Tooltip("FlockAgent 分离距离")]
        public float flockSeparationDistance = 2f;

        [Tooltip("FlockAgent 旋转速度")]
        public float flockRotationSpeed = 5f;
    }
}
