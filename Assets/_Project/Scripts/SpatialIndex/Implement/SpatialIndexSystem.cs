using Unity.Entities;

namespace SpatialIndexing
{
    /// <summary>World 内共享网格的所有者；群游与托管查询读取同一份敌人索引。</summary>
    [DisableAutoCreation]
    internal partial class SpatialIndexSystem : SystemBase
    {
        internal readonly ContinuousSpatialGrid Enemies = new();
        internal SpatialQueryService Service;

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
            Enemies.Dispose();
        }
    }
}
