using System;
using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using FlockingSystem.ECS;
using Interfaces;
using Services;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VContainer;

namespace SpatialIndexing
{
    /// <summary>
    /// 场景级查询入口。敌人和子弹使用分类连续索引；玩家通过直接引用查询。
    /// 查询前等待群游读者并同步宿主移动，死亡对象在返回结果时再次校验。
    /// </summary>
    public sealed class SpatialQueryService : ISpatialQueryService, IProjectileQueryService,
        IProjectileSnapshotPublisher, IDisposable
    {
        private readonly List<IDamageable> _enemies = new();
        private readonly List<IDamageable> _results = new();
        private readonly Stack<int> _free = new();
        private readonly List<int> _candidates = new();
        private readonly ContinuousSpatialGrid _projectiles = new();
        private readonly List<ProjectileHandle> _projectileHandles = new();
        private readonly IPlayerCombatTarget _player;
        private ContinuousSpatialGrid _enemiesGrid;
        private SpatialIndexSystem _system;
        private int _enemyCaptureFrame = -1;
        private int _enemyBuildFrame = -1;

        public SpatialQueryService()
        {
        }

        /// <summary>从当前容器尝试解析玩家目标；独立测试场景可以不提供玩家。</summary>
        [Inject]
        public SpatialQueryService(IObjectResolver resolver)
        {
            _player = resolver.TryResolve(out IPlayerCombatTarget player) ? player : null;
        }

        internal ContinuousSpatialGrid EnemyGrid
        {
            get
            {
                if (_enemiesGrid == null)
                {
                    World world = World.DefaultGameObjectInjectionWorld;
                    if (world != null && world.IsCreated)
                    {
                        _system = world.GetOrCreateSystemManaged<SpatialIndexSystem>();
                        _system.Service = this;
                        _enemiesGrid = _system.Enemies;
                    }
                    else
                    {
                        _enemiesGrid = new ContinuousSpatialGrid();
                    }
                    _enemiesGrid.Configure(new float3(-100), 25f, new int3(8), 16);
                }
                return _enemiesGrid;
            }
        }

        /// <summary>分配查询槽位，与 Flock 的实体槽位分开管理。</summary>
        internal int RegisterEnemy(IDamageable enemy)
        {
            int existing = _enemies.IndexOf(enemy);
            if (existing >= 0)
            {
                return existing;
            }
            int slot = _free.Count > 0 ? _free.Pop() : _enemies.Count;
            if (slot == _enemies.Count)
            {
                _enemies.Add(enemy);
            }
            else
            {
                _enemies[slot] = enemy;
            }
            ContinuousSpatialGrid grid = EnemyGrid;
            grid.Configure(grid.Layout.Origin, grid.Layout.CellSize, grid.Layout.Dimensions, _enemies.Count);
            _enemyCaptureFrame = -1;
            _enemyBuildFrame = -1;
            return slot;
        }

        /// <summary>立即失效死亡或归还的对象，先等待正在读取快照的 Job。</summary>
        internal void UnregisterEnemy(int slot)
        {
            if ((uint)slot >= (uint)_enemies.Count || _enemies[slot] == null)
            {
                return;
            }

            ContinuousSpatialGrid grid = _enemiesGrid;
            if (grid != null && grid.Active.IsCreated && (uint)slot < (uint)grid.Active.Length)
            {
                grid.Complete();
                grid.Active[slot] = 0;
            }

            _enemies[slot] = null;
            _free.Push(slot);
            _enemyCaptureFrame = -1;
            _enemyBuildFrame = -1;
        }

        /// <summary>批量读取宿主姿态；外部移动与倍率在下一次群游采样前生效。</summary>
        internal void CaptureEnemies()
        {
            if (Application.isPlaying && _enemyCaptureFrame == Time.frameCount &&
                _enemiesGrid != null && _enemiesGrid.Active.IsCreated)
            {
                return;
            }

            _system?.EntityManager.CompleteAllTrackedJobs();
            ContinuousSpatialGrid grid = EnemyGrid;
            grid.Complete();
            if (!grid.Active.IsCreated || grid.Active.Length < _enemies.Count)
            {
                grid.Configure(grid.Layout.Origin, grid.Layout.CellSize, grid.Layout.Dimensions, _enemies.Count);
            }

            for (int slot = 0; slot < _enemies.Count; slot++)
            {
                IDamageable target = _enemies[slot];
                bool alive = IsAlive(target);
                grid.Active[slot] = alive ? (byte)1 : (byte)0;
                if (alive)
                {
                    grid.Positions[slot] = target.Position;
                }
            }

            _enemyCaptureFrame = Application.isPlaying ? Time.frameCount : -1;
            _enemyBuildFrame = -1;
        }

        /// <summary>标记 Flocking 已经构建当前帧可复用的敌人网格。</summary>
        internal void MarkEnemyGridBuilt()
        {
            _enemyBuildFrame = Application.isPlaying ? Time.frameCount : -1;
        }

        /// <summary>敌人 Transform 由提交系统更新后，使本帧旧快照失效。</summary>
        internal void InvalidateEnemySnapshot()
        {
            _enemyCaptureFrame = -1;
            _enemyBuildFrame = -1;
        }

        private void EnsureEnemyGrid()
        {
            CaptureEnemies();
            if (!Application.isPlaying || _enemyBuildFrame != Time.frameCount)
            {
                EnemyGrid.Build();
                _enemyBuildFrame = Application.isPlaying ? Time.frameCount : -1;
            }
        }

        public IDamageable QueryNearest(Vector3 center, float radius, int layerMask)
        {
            List<IDamageable> targets = QueryRadius(center, radius, layerMask);
            IDamageable nearest = null;
            float distance = float.PositiveInfinity;
            foreach (IDamageable target in targets)
            {
                float squared = (target.Position - center).sqrMagnitude;
                if (squared < distance)
                {
                    distance = squared;
                    nearest = target;
                }
            }
            return nearest;
        }

        public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask)
        {
            List<IDamageable> result = _results;
            result.Clear();
            if (radius < 0f)
            {
                return result;
            }
            if ((layerMask & SpatialLayers.Enemy) != 0)
            {
                EnsureEnemyGrid();
                EnemyGrid.Query(center, radius, _candidates);
                foreach (int slot in _candidates)
                {
                    IDamageable target = _enemies[slot];
                    if (IsAlive(target))
                    {
                        result.Add(target);
                    }
                }
            }
            if ((layerMask & SpatialLayers.Player) != 0 && IsAlive(_player) &&
                (_player.Position - center).sqrMagnitude <= radius * radius)
            {
                result.Add(_player);
            }
            return result;
        }

        public List<IDamageable> QueryAll(int layerMask)
        {
            List<IDamageable> result = _results;
            result.Clear();
            if ((layerMask & SpatialLayers.Enemy) != 0)
            {
                foreach (IDamageable target in _enemies)
                {
                    if (IsAlive(target))
                    {
                        result.Add(target);
                    }
                }
            }
            if ((layerMask & SpatialLayers.Player) != 0 && IsAlive(_player))
            {
                result.Add(_player);
            }
            return result;
        }

        /// <summary>中央模拟完成后一次发布子弹快照，不给每颗子弹创建组件。</summary>
        public void PublishProjectiles(IReadOnlyList<ProjectileRuntime> projectiles)
        {
            _projectiles.Configure(new float3(-100), 25f, new int3(8), projectiles.Count);
            _projectileHandles.Clear();
            for (int slot = 0; slot < _projectiles.Active.Length; slot++)
            {
                _projectiles.Active[slot] = 0;
            }
            for (int slot = 0; slot < projectiles.Count; slot++)
            {
                ProjectileRuntime projectile = projectiles[slot];
                _projectileHandles.Add(new ProjectileHandle(projectile));
                _projectiles.Active[slot] = projectile.IsCompleted ? (byte)0 : (byte)1;
                _projectiles.Positions[slot] = projectile.Position;
            }
            _projectiles.Build();
        }

        /// <summary>查询最近一次完整模拟步的子弹位置，排除快照后已归还的句柄。</summary>
        public void QueryProjectiles(Vector3 center, float radius, List<ProjectileHandle> results)
        {
            results.Clear();
            _projectiles.Query(center, radius, _candidates);
            foreach (int slot in _candidates)
            {
                ProjectileHandle handle = _projectileHandles[slot];
                if (handle.Resolve() != null)
                {
                    results.Add(handle);
                }
            }
        }

        /// <summary>读取一次有效租借的当前位置；回收或复用后返回 false。</summary>
        public bool TryGetPosition(ProjectileHandle handle, out Vector3 position)
        {
            ProjectileRuntime projectile = handle.Resolve();
            position = projectile != null ? projectile.Position : default;
            return projectile != null;
        }

        private static bool IsAlive(IDamageable target) => target != null &&
            (!(target is UnityEngine.Object obj) || obj != null) && target.IsAlive;

        /// <summary>解除 World 服务引用并等待所有网格使用者。</summary>
        public void Dispose()
        {
            _projectiles.Dispose();
            if (_system != null)
            {
                _system.Service = null;
                _system.Enemies.Complete();
                if (_system.Enemies.Active.IsCreated)
                {
                    for (int i = 0; i < _system.Enemies.Active.Length; i++)
                    {
                        _system.Enemies.Active[i] = 0;
                    }
                }
            }
            else
            {
                _enemiesGrid?.Dispose();
            }
            _enemies.Clear();
            _results.Clear();
        }
    }
}
