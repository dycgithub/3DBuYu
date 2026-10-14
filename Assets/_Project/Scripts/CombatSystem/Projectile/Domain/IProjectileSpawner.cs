namespace CombatSystem
{
    /// <summary>提交投射物生成请求的边界接口。</summary>
    public interface IProjectileSpawner
    {
        /// <summary>尝试生成一个投射物。</summary>
        /// <param name="projectileData">待生成的投射物数据。</param>
        /// <returns>生成请求被模拟服务接受时返回 <c>true</c>。</returns>
        bool TrySpawnProjectile(in ProjectileData projectileData);
        /// <summary>尝试生成指定数量的投射物。</summary>
        /// <param name="projectileData">每个投射物使用的数据模板。</param>
        /// <param name="projectileCount">要生成的数量。</param>
        /// <returns>生成请求被模拟服务接受时返回 <c>true</c>。</returns>
        bool TrySpawnProjectileBatch(in ProjectileData projectileData, int projectileCount);
    }
}
