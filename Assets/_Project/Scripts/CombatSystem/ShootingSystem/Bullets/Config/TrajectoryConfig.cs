using UnityEngine;

namespace CombatSystem
{
    /// <summary>描述子弹是否即时命中以及飞行方向如何更新。</summary>
    public abstract class TrajectoryConfig : ScriptableObject
    {
        public abstract bool IsHitscan { get; }

        /// <summary>
        /// 根据当前子弹状态返回下一次移动方向。直线轨迹直接返回输入方向。
        /// </summary>
        public abstract Vector3 GetDirection(ProjectileRuntime projectile, Vector3 currentDirection);
    }
}
