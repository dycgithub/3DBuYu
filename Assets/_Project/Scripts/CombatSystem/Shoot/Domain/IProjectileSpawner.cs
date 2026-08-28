namespace CombatSystem
{
    public interface IProjectileSpawner
    {
        bool TrySpawn(in ProjectileInfo info);
        bool TrySpawnBatch(in ProjectileInfo info, int count);
    }
}
