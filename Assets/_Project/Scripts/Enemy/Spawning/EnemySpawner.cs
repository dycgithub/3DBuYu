using System.Collections.Generic;
using R3;
using Services;
using VContainer.Unity;
using UnityEngine;

namespace EnemySystem.Spawning
{
    /// <summary>管理敌人生成、死亡通知和死亡表现完成后的对象池回收。</summary>
    public class EnemySpawner : IEnemySpawner, IEnemyChildSpawner, ITickable
    {
        private readonly EnemyFactory _factory;
        private readonly ISpawnPositionProvider _spawnPositionProvider;
        private readonly HashSet<Enemy> _activeEnemies = new();
        private readonly Dictionary<Enemy, EnemyRank> _ranks = new();
        private readonly int[] _rankCounts = new int[3];
        private readonly HashSet<Enemy> _pendingRelease = new();
        private readonly Dictionary<Enemy, float> _pendingReleaseTimes = new();
        private readonly List<Enemy> _iterationBuffer = new(128);
        private readonly List<Enemy> _releaseBuffer = new(32);
        private readonly ReactiveProperty<int> _activeEnemyCount = new(0);

        public int ActiveEnemyCount => _activeEnemyCount.Value;
        public Observable<int> OnActiveEnemyCountChanged => _activeEnemyCount;

        /// <summary>
        /// 创建敌人生成器并注入对象工厂和出生点提供器。
        /// </summary>
        /// <param name="factory">负责创建和回收敌人的工厂。</param>
        /// <param name="spawnPositionProvider">负责计算敌人出生位置的服务。</param>
        public EnemySpawner(EnemyFactory factory, ISpawnPositionProvider spawnPositionProvider)
        {
            _factory = factory;
            _spawnPositionProvider = spawnPositionProvider;
        }

        /// <summary>
        /// 按独立配额分类创建敌人，成功后立即更新总数与分类数。
        /// </summary>
        /// <param name="prefab">已配置的怪物预制体。</param>
        /// <param name="rank">普通、精英或 Boss 的数量分类。</param>
        /// <param name="healthMultiplier">难度生命倍率。</param>
        /// <param name="speedMultiplier">难度速度倍率。</param>
        /// <returns>实际生成成功时返回 true。</returns>
        public bool TrySpawnEnemy(Enemy prefab, EnemyRank rank, float healthMultiplier, float speedMultiplier)
        {
            if (prefab == null)
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
            _ranks.Add(enemy, rank);
            _rankCounts[(int)rank]++;
            _activeEnemyCount.Value = _activeEnemies.Count;
            return true;
        }

        /// <summary>生成一个不占用主波次 rank 的子敌人。</summary>
        public Enemy TrySpawnChild(
            Enemy parent,
            Enemy prefab,
            Vector3 position,
            float healthMultiplier,
            float speedMultiplier)
        {
            if (parent == null || prefab == null)
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
            _activeEnemyCount.Value = _activeEnemies.Count;
            return enemy;
        }

        /// <summary>按生成等级统计，不把群游的 Fast、Tank 类型当作数量等级。</summary>
        public int CountRank(EnemyRank rank) => _rankCounts[(int)rank];

        /// <summary>
        /// 从存活集合移除死亡敌人，并延迟到死亡高亮完成后回收。
        /// </summary>
        /// <param name="enemy">已死亡的敌人。</param>
        private void HandleEnemyDied(Enemy enemy)
        {
            enemy.OnDied -= HandleEnemyDied;
            _activeEnemies.Remove(enemy);
            if (_ranks.Remove(enemy, out EnemyRank rank))
            {
                _rankCounts[(int)rank]--;
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
            if (_pendingRelease.Count == 0)
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
            ReleasePendingEnemies();
            CopyActiveEnemies();
            for (int index = 0; index < _iterationBuffer.Count; index++)
            {
                Enemy enemy = _iterationBuffer[index];
                enemy.OnDied -= HandleEnemyDied;
                _factory.ReleaseEnemy(enemy);
            }

            _iterationBuffer.Clear();
            _activeEnemies.Clear();
            _ranks.Clear();
            System.Array.Clear(_rankCounts, 0, _rankCounts.Length);
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
            CopyActiveEnemies();
            for (int index = 0; index < _iterationBuffer.Count; index++)
            {
                _iterationBuffer[index].Kill();
            }

            _iterationBuffer.Clear();
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
    }
}
