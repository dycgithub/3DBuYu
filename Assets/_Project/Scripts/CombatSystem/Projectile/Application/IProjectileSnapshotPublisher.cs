using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>接收投射物模拟完成后的空间快照。</summary>
    public interface IProjectileSnapshotPublisher
    {
        /// <summary>发布一次完整模拟步后的活动投射物位置快照。</summary>
        void PublishProjectiles(IReadOnlyList<ProjectileState> projectiles);
    }
}
