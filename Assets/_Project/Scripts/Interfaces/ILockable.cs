using UnityEngine;

namespace Interfaces
{
    /// <summary>
    /// 可被锁定系统锁定的目标分类。
    /// </summary>
    public enum TargetCategory
    {
        Normal,        // 普通单位
        Boss,       // Boss 单位
        Structure   // 建筑/结构
    }

    /// <summary>
    /// 扩展的可锁定目标接口。
    /// 任何希望在锁定系统中参与的 IDamageable 也应实现此接口。
    /// </summary>
    public interface ILockable : IDamageable
    {
        /// <summary>威胁等级 0-100，越高越危险。</summary>
        float ThreatLevel { get; }

        /// <summary>当前是否可以被锁定（隐身/死亡时返回 false）。</summary>
        bool IsLockable { get; }

        /// <summary>锁定准星的锚点（世界空间坐标，通常是头部/身体中心偏上）。</summary>
        Vector3 LockAnchorPoint { get; }

        /// <summary>目标分类，用于过滤和优先级调整。</summary>
        TargetCategory Category { get; }

        /// <summary>生命值百分比 0-1，用于自动瞄准评分。</summary>
        float HealthPercent { get; }
    }
}
