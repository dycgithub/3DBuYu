namespace CombatSystem
{
    /// <summary>攻击系统生成逻辑子弹的最小抽象。</summary>
    public interface IProjectileSpawner
    {
        bool TrySpawn(in ProjectileInfo info);
        bool TrySpawnBatch(in ProjectileInfo info, int count);
    }
}
