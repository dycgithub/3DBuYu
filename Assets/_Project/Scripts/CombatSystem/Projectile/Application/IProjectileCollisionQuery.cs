using Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>查询投射物沿路径遇到的有效伤害目标。</summary>
    public interface IProjectileCollisionQuery
    {
        /// <summary>尝试沿投射物移动距离执行球形扫掠。</summary>
        /// <param name="projectile">待查询的投射物状态。</param>
        /// <param name="travelDistance">本次模拟要查询的移动距离。</param>
        /// <param name="hit">命中时输出最近物理命中信息。</param>
        /// <param name="target">命中时输出有效伤害目标。</param>
        /// <returns>找到有效目标时返回 <c>true</c>。</returns>
        bool TryFindSweepCollision(
            ProjectileState projectile,
            float travelDistance,
            out RaycastHit hit,
            out IDamageable target);
        /// <summary>尝试沿 Hitscan 路径查找最近目标。</summary>
        /// <param name="projectileData">包含起点、方向和最大距离的投射物数据。</param>
        /// <param name="hit">命中时输出最近物理命中信息。</param>
        /// <param name="target">命中时输出有效伤害目标。</param>
        /// <returns>找到有效目标时返回 <c>true</c>。</returns>
        bool TryFindRaycastCollision(ProjectileData projectileData, out RaycastHit hit, out IDamageable target);
    }

    /// <summary>为连续范围投射物提供按距离排序的多目标扫掠查询。</summary>
    public interface IProjectileCollisionRangeQuery
    {
        /// <summary>收集本次扫掠中按命中距离排序的唯一目标。</summary>
        int QuerySweepCollisions(
            ProjectileState projectile,
            float travelDistance,
            List<ProjectileCollision> collisions);
    }

    /// <summary>记录一个投射物扫掠命中及其有效目标。</summary>
    public readonly struct ProjectileCollision
    {
        /// <summary>物理命中数据。</summary>
        public RaycastHit Hit { get; }
        /// <summary>统一伤害目标。</summary>
        public IDamageable Target { get; }

        /// <summary>创建一个投射物命中结果。</summary>
        public ProjectileCollision(RaycastHit hit, IDamageable target)
        {
            Hit = hit;
            Target = target;
        }
    }
}
