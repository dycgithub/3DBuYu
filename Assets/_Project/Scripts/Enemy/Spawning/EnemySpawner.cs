using System;
using System.Collections.Generic;
using EffectSystem;
using R3;
using Services;
using Unity.Profiling;
using VContainer.Unity;
using UnityEngine;

namespace EnemySystem.Spawning
{
    /// <summary>所有分类共享 2000 存活上限；死亡立即释放人口，表现结束后归还对象池。</summary>
    public class EnemySpawner : IEnemySpawner, IEnemyChildSpawner, ITickable, IDisposable
    {
        private static readonly ProfilerMarker TickMarker =
            new("3DBuYu/VContainer/EnemySpawner.Tick");

        private readonly EnemyFactory _factory;
        private readonly ISpawnPositionProvider _spawnPositionProvider;
        private readonly IEffectService _effectService;
        private readonly HashSet<Enemy> _activeEnemies = new();
        private readonly Dictionary<Enemy, EnemySpawnCategory> _categories = new();
        private readonly int[] _categoryCounts = new int[4];
        private readonly HashSet<Enemy> _pendingRelease = new();
        private readonly Dictionary<Enemy, float> _pendingReleaseTimes = new();
        private readonly List<Enemy> _iterationBuffer = new(128);
        private readonly List<Enemy> _releaseBuffer = new(32);
        private readonly ReactiveProperty<int> _activeEnemyCount = new(0);
        private bool _disposed;

        public int ActiveEnemyCount => _disposed ? 0 : _activeEnemyCount.Value;
        public Observable<int> OnActiveEnemyCountChanged => _activeEnemyCount;

        /// <summary>
        /// 创建敌人生成器并注入对象工厂和出生点提供器。
        /// </summary>
        /// <param name="factory">负责创建和回收敌人的工厂。</param>
        /// <param name="spawnPositionProvider">负责计算敌人出生位置的服务。</param>
        public EnemySpawner(
            EnemyFactory factory,
            ISpawnPositionProvider spawnPositionProvider,
            IEffectService effectService = null)
        {
            _factory = factory;
            _spawnPositionProvider = spawnPositionProvider;
            _effectService = effectService;
        }

        /// <summary>
        /// 按生成分类创建敌人，成功后立即更新总数与分类数。
        /// </summary>
        /// <param name="prefab">已配置的怪物预制体。</param>
        /// <param name="category">普通、精英或 Boss 的生成分类。</param>
        /// <param name="healthMultiplier">难度生命倍率。</param>
        /// <param name="speedMultiplier">难度速度倍率。</param>
        /// <returns>实际生成成功时返回 true。</returns>
        public bool TrySpawnEnemy(
            Enemy prefab,
            EnemySpawnCategory category,
            float healthMultiplier,
            float speedMultiplier)
        {
            if (_disposed || prefab == null || category == EnemySpawnCategory.Child ||
                _activeEnemies.Count >= EnemyPopulationLimits.MaximumAlive)
            {
                return false;
            }

            Vector3 spawnPosition = _spawnPositionProvider.GetSpawnPosition();
            Enemy enemy = _factory.CreateEnemy(
                prefab.gameObject,
                spawnPosition,
                healthMultiplier, speedMultiplier);
            if (enemy == null)
            {
                return false;
            }

            enemy.OnDied += HandleEnemyDied;
            _activeEnemies.Add(enemy);
            _categories.Add(enemy, category);
            _categoryCounts[(int)category]++;
            _activeEnemyCount.Value = _activeEnemies.Count;
            PlaySpawnEffect(spawnPosition);
            return true;
        }

        /// <summary>子敌人不占普通目标，但与母体共享总存活上限。</summary>
        public Enemy TrySpawnChild(
            Enemy parent,
            Enemy prefab,
            Vector3 position,
            float healthMultiplier,
            float speedMultiplier)
        {
            if (_disposed || parent == null || prefab == null ||
                _activeEnemies.Count >= EnemyPopulationLimits.MaximumAlive)
            {
                return null;
            }

            Enemy enemy = _factory.CreateEnemy(
                prefab.gameObject,
                position,
                healthMultiplier,
                speedMultiplier);
            if (enemy == null)
            {
                return null;
            }

            enemy.OnDied += HandleEnemyDied;
            _activeEnemies.Add(enemy);
            _categories.Add(enemy, EnemySpawnCategory.Child);
            _categoryCounts[(int)EnemySpawnCategory.Child]++;
            _activeEnemyCount.Value = _activeEnemies.Count;
            PlaySpawnEffect(position);
            return enemy;
        }

        /// <summary>按生成分类读取当前存活数量。</summary>
        public int CountCategory(EnemySpawnCategory category)
        {
            return _disposed ? 0 : _categoryCounts[(int)category];
        }

        /// <summary>
        /// 从存活集合移除死亡敌人，并延迟到死亡高亮完成后回收。
        /// </summary>
        /// <param name="enemy">已死亡的敌人。</param>
        private void HandleEnemyDied(Enemy enemy)
        {
            enemy.OnDied -= HandleEnemyDied;
            _activeEnemies.Remove(enemy);
            if (_categories.Remove(enemy, out EnemySpawnCategory category))
            {
                _categoryCounts[(int)category]--;
            }
            // 与死亡表现的游戏时间保持一致；暂停期间保留完整的高亮时长。
            _pendingRelease.Add(enemy);
            _pendingReleaseTimes[enemy] = Time.time + enemy.DeathHighlightDuration;
            _activeEnemyCount.Value = _activeEnemies.Count;
        }

        /// <summary>
        /// 回收本帧等待释放的敌人。
        /// </summary>
        public void Tick()
        {
            using var profilerScope = TickMarker.Auto();
            if (_disposed || _pendingRelease.Count == 0)
            {
                return;
            }

            float currentTime = Time.time;
            _releaseBuffer.Clear();
            foreach (Enemy enemy in _pendingRelease)
            {
                if (!_pendingReleaseTimes.TryGetValue(enemy, out float releaseTime) ||
                    currentTime >= releaseTime)
                {
                    _releaseBuffer.Add(enemy);
                }
            }

            for (int index = 0; index < _releaseBuffer.Count; index++)
            {
                Enemy enemy = _releaseBuffer[index];
                _factory.ReleaseEnemy(enemy);
                _pendingRelease.Remove(enemy);
                _pendingReleaseTimes.Remove(enemy);
            }

            _releaseBuffer.Clear();
        }

        /// <summary>
        /// 立即回收所有当前存活和等待释放的敌人。
        /// </summary>
        public void ClearAll()
        {
            if (_disposed)
            {
                return;
            }

            ReleasePendingEnemies();
            CopyActiveEnemies();
            for (int index = 0; index < _iterationBuffer.Count; index++)
            {
                Enemy enemy = _iterationBuffer[index];
                if (enemy != null)
                {
                    enemy.OnDied -= HandleEnemyDied;
                }

                _factory.ReleaseEnemy(enemy);
            }

            _iterationBuffer.Clear();
            _activeEnemies.Clear();
            _categories.Clear();
            System.Array.Clear(_categoryCounts, 0, _categoryCounts.Length);
            _activeEnemyCount.Value = 0;
        }

        /// <summary>立即回收所有等待死亡表现完成的敌人。</summary>
        private void ReleasePendingEnemies()
        {
            _releaseBuffer.Clear();
            foreach (Enemy enemy in _pendingRelease)
            {
                _releaseBuffer.Add(enemy);
            }

            for (int index = 0; index < _releaseBuffer.Count; index++)
            {
                _factory.ReleaseEnemy(_releaseBuffer[index]);
            }

            _pendingRelease.Clear();
            _pendingReleaseTimes.Clear();
            _releaseBuffer.Clear();
        }

        /// <summary>
        /// 请求所有当前存活敌人执行死亡流程。
        /// </summary>
        public void KillAllEnemies()
        {
            if (_disposed)
            {
                return;
            }

            CopyActiveEnemies();
            for (int index = 0; index < _iterationBuffer.Count; index++)
            {
                Enemy enemy = _iterationBuffer[index];
                if (enemy != null)
                {
                    enemy.Kill();
                }
            }

            _iterationBuffer.Clear();
        }

        /// <summary>场景子容器释放时立即回收所有仍被生成器持有的敌人。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ClearAll();
            _disposed = true;
            _activeEnemyCount.Dispose();
        }

        /// <summary>
        /// 将存活集合复制到可安全遍历的临时缓冲区。
        /// </summary>
        private void CopyActiveEnemies()
        {
            _iterationBuffer.Clear();
            foreach (Enemy enemy in _activeEnemies)
            {
                _iterationBuffer.Add(enemy);
            }
        }

        /// <summary>在敌人确认生成后播放出生位置特效。</summary>
        private void PlaySpawnEffect(Vector3 position)
        {
            _effectService?.Play(EffectId.EnemySpawn, position);
        }
    }
}
