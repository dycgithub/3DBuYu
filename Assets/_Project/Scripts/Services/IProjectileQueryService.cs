using System.Collections.Generic;
using SpatialIndexing;
using UnityEngine;

namespace Services
{
    /// <summary>查询仍由托管模拟器驱动的活动投射物空间快照。</summary>
    public interface IProjectileQueryService
    {
        /// <summary>查询半径内仍然有效的投射物句柄。</summary>
        void QueryProjectiles(Vector3 center, float radius, List<ProjectileHandle> results);

        /// <summary>读取句柄对应的当前位置；对象池复用后句柄失效。</summary>
        bool TryGetPosition(ProjectileHandle handle, out Vector3 position);
    }
}
