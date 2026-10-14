using CombatSystem;

namespace SpatialIndexing
{
    /// <summary>引用与租借代数共同标识一次飞行，池对象复用不会让旧句柄重新有效。</summary>
    public readonly struct ProjectileHandle
    {
        private readonly ProjectileState _projectile;
        private readonly ulong _generation;

        internal ProjectileHandle(ProjectileState projectile)
        {
            _projectile = projectile;
            _generation = projectile.Generation;
        }

        internal ProjectileState Resolve() => _projectile != null &&
            !_projectile.IsCompleted && _projectile.Generation == _generation ? _projectile : null;
    }
}
