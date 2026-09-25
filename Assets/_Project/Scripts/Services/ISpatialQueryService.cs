using System.Collections.Generic;
using UnityEngine;
using Interfaces;

namespace Services
{
    /// <summary>
    /// 只读伤害目标查询。注册与批量同步由空间服务和实体生命周期负责。
    /// </summary>
    public interface ISpatialQueryService
    {
        /// <summary>查询最近的匹配实体。</summary>
        IDamageable QueryNearest(Vector3 center, float radius, int layerMask);

        /// <summary>查询范围内的所有匹配实体。</summary>
        List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask);

        /// <summary>查询所有已注册且匹配层级的实体，不受空间网格范围和结果缓冲区限制。</summary>
        List<IDamageable> QueryAll(int layerMask);
    }
}
