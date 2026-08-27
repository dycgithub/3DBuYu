namespace CombatSystem
{
    /// <summary>把统一生成接口转发给场景内的集中式子弹模拟器。</summary>
    public sealed class PooledBulletSpawner : IProjectileSpawner
    {
        private readonly ProjectileSimulationService _simulation;

        public PooledBulletSpawner(ProjectileSimulationService simulation)
        {
            _simulation = simulation;
        }

        public bool TrySpawn(in ProjectileInfo info)
        {
            return _simulation != null && _simulation.TrySpawn(info);
        }

        public bool TrySpawnBatch(in ProjectileInfo info, int count)
        {
            return _simulation != null && _simulation.TrySpawnBatch(info, count);
        }
    }
}
