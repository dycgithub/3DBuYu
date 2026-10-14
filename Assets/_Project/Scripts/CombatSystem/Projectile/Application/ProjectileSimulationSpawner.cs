using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>把直接生成及整批准备请求转交给模拟服务，准备失败不产生活动投射物。</summary>
    public sealed class ProjectileSimulationSpawner : IProjectileSpawner, IProjectileBatchSpawner
    {
        private readonly ProjectileSimulationService _simulation;

        /// <summary>创建投射物模拟生成器。</summary>
        /// <param name="simulation">负责推进投射物生命周期的模拟服务。</param>
        public ProjectileSimulationSpawner(ProjectileSimulationService simulation)
        {
            _simulation = simulation;
        }

        /// <summary>先租借完整批次，交由整卡资源事务决定提交或取消。</summary>
        public bool TryPrepare(IReadOnlyList<ProjectileData> data, out IPreparedProjectileBatch batch)
        {
            batch = null;
            return _simulation != null && _simulation.TryPrepare(data, out batch);
        }

        /// <summary>尝试生成一个投射物。</summary>
        /// <param name="projectileData">待生成的投射物数据。</param>
        /// <returns>模拟服务接受请求时返回 <c>true</c>。</returns>
        public bool TrySpawnProjectile(in ProjectileData projectileData)
        {
            return _simulation != null && _simulation.TrySpawnProjectile(projectileData);
        }

        /// <summary>尝试生成指定数量的投射物。</summary>
        /// <param name="projectileData">每个投射物使用的数据模板。</param>
        /// <param name="projectileCount">要生成的投射物数量。</param>
        /// <returns>模拟服务接受请求时返回 <c>true</c>。</returns>
        public bool TrySpawnProjectileBatch(in ProjectileData projectileData, int projectileCount)
        {
            return _simulation != null &&
                   _simulation.TrySpawnProjectileBatch(projectileData, projectileCount);
        }
    }
}
