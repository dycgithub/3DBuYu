using UnityEngine;

namespace Interfaces
{
    /// <summary>
    /// 可受伤害实体的契约。
    /// 将伤害系统与具体敌人实现解耦。
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 获取实体的世界坐标。
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// 获取一个值，指示实体是否存活。
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// 获取实体的 Transform 组件。
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// 对实体造成伤害。
        /// </summary>
        /// <param name="amount">伤害值。</param>
        void TakeDamage(float amount);
    }
}
