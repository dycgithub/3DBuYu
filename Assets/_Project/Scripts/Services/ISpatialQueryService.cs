using System.Collections.Generic;
using UnityEngine;
using Interfaces;

namespace Services
{
    /// <summary>
    /// 空间查询服务接口。
    /// 替代直接调用 <see cref="SpatialSystem.Bridge.SpatialRegistry.Instance"/>，
    /// 使 EnemyBase、Bullet、AimTargetProvider 等通过依赖注入获取查询能力。
    /// </summary>
    public interface ISpatialQueryService
    {
        /// <summary>注册一个实体到空间网格。</summary>
        int Register(IDamageable entity, float radius, int layerMask);

        /// <summary>从空间网格注销。</summary>
        void Unregister(int entityId);

        /// <summary>更新实体位置（仅在位置变化时调用）。</summary>
        void UpdatePosition(int entityId, Vector3 position);

        /// <summary>查询最近的匹配实体。</summary>
        IDamageable QueryNearest(Vector3 center, float radius, int layerMask);

        /// <summary>查询范围内的所有匹配实体。</summary>
        List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask);

        /// <summary>查询所有已注册且匹配层级的实体，不受空间网格范围和结果缓冲区限制。</summary>
        List<IDamageable> QueryAll(int layerMask);
    }
}
