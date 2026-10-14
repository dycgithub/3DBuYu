using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>描述投射物移动或 Hitscan 查询使用的轨迹规则。</summary>
    public abstract class TrajectoryDefinitionSO : ScriptableObject
    {
        /// <summary>指示当前轨迹是否直接执行一次射线命中查询。</summary>
        public abstract bool IsHitscan { get; }

        /// <summary>根据当前运行状态计算下一步移动方向。</summary>
        public virtual Vector3 GetDirection(ProjectileState projectile, Vector3 currentDirection)
        {
            return currentDirection;
        }

        /// <summary>结合可选空间查询服务计算下一步移动方向。</summary>
        public virtual Vector3 GetDirection(
            ProjectileState projectile,
            Vector3 currentDirection,
            ISpatialQueryService spatialQueryService)
        {
            return GetDirection(projectile, currentDirection);
        }

        /// <summary>按轨迹规则调整生成时的投射物数据。</summary>
        public virtual void ConfigureData(ref ProjectileData projectileData)
        {
        }

        /// <summary>初始化轨迹所需的投射物运行时状态。</summary>
        public virtual void InitializeRuntime(
            ProjectileState projectile,
            ISpatialQueryService spatialQueryService)
        {
        }

        /// <summary>指示达到最大距离后轨迹是否仍可继续。</summary>
        public virtual bool CanContinueBeyondTravelLimit(ProjectileState projectile)
        {
            return false;
        }

        /// <summary>判断轨迹是否结束自身生命周期。</summary>
        public virtual bool ShouldComplete(ProjectileState projectile)
        {
            return false;
        }

        /// <summary>指示命中后是否继续查询其他目标。</summary>
        public virtual bool ContinuesAfterHit => false;
    }
}
