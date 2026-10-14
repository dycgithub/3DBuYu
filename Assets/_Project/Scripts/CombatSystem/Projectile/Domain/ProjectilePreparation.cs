using System;
using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>完整准备的一批投射物；提交只发布已有状态，销毁则取消未提交内容。</summary>
    public interface IPreparedProjectileBatch : IDisposable
    {
        void Commit();
    }

    /// <summary>在来源消费前准备完整发射请求，失败不发布任何弹体。</summary>
    public interface IProjectileBatchPreparer
    {
        bool TryPrepare(IReadOnlyList<ProjectileLaunchRequest> requests, out IPreparedProjectileBatch batch);
    }

    /// <summary>模拟生成边界准备规范化数据，不进入活动模拟及空间索引。</summary>
    public interface IProjectileBatchSpawner
    {
        bool TryPrepare(IReadOnlyList<ProjectileData> data, out IPreparedProjectileBatch batch);
    }
}
