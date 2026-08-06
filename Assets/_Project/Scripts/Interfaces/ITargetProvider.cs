using UnityEngine;

namespace Interfaces
{
    /// <summary>
    /// 目标搜索服务契约。
    /// 将目标查找逻辑与具体实现（Tag 查找 / 物理 Overlap 等）解耦。
    /// </summary>
    public interface ITargetProvider
    {
        /// <summary>
        /// 在指定范围内搜索最近的受伤害目标。
        /// </summary>
        /// <param name="origin">搜索起点。</param>
        /// <param name="range">搜索半径。</param>
        /// <returns>最近的目标；若未找到则返回 <see langword="null"/>。</returns>
        IDamageable GetNearestTarget(Vector3 origin, float range);
    }
}
