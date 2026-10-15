using System;
using System.Collections.Generic;
using EffectSystem;
using EnemySystem;
using EnemySystem.Components;
using Interfaces;
using R3;
using Services;
using Unity.Profiling;
using VContainer.Unity;
using UnityEngine;

namespace EnemySystem.Spawning
{
    /// <summary>共享管理敌人存活和回收，并在死亡表现结束后派发已到账的奖励特效。</summary>
    public class EnemySpawner : IEnemySpawner, IEnemyChildSpawner, ITickable, IDisposable
    {
        private const float ManualEffectLifetime = 0f;

        private static readonly ProfilerMarker TickMarker =
            new("3DBuYu/VContainer/EnemySpawner.Tick");

        private sealed class RewardFlight
        {
            public readonly GameObject Effect;
            public readonly ParticleSystem Particles;
            public readonly float Speed;
            public readonly float ArrivalDistance;

            public RewardFlight(
                GameObject effect,
                ParticleSystem particles,
                float speed,
                float arrivalDistance)
            {
                Effect = effect;
                Particles = particles;
                Speed = speed;
                ArrivalDistance = arrivalDistance;
            }
        }

        private readonly EnemyFactory _factory;
        private readonly ISpawnPositionProvider _spawnPositionProvider;
        private readonly EnemyPopulationSettingsSO _populationSettings;
        private readonly IEffectService _effectService;
        private readonly CombatEffectCatalogSO _effectCatalog;
        private readonly IPooledEffectService _pooledEffectService;
        private readonly IPlayerCombatTarget _playerTarget;
        private readonly HashSet<Enemy> _activeEnemies = new();
        private readonly int[] _categoryCounts = new int[4];
        private readonly HashSet<Enemy> _pendingRelease = new();
        private readonly Dictionary<Enemy, float> _pendingReleaseTimes = new();
        private readonly List<Enemy> _iterationBuffer = new(128);
        private readonly List<Enemy> _releaseBuffer = new(32);
        private readonly List<RewardFlight> _rewardFlights = new();
        private readonly ReactiveProperty<int> _activeEnemyCount = new(0);
        private bool _disposed;

        public int ActiveEnemyCount => _disposed ? 0 : _activeEnemyCount.Value;
        public Observable<int> OnActiveEnemyCountChanged => _activeEnemyCount;

        /// <summary>
        /// 创建敌人生成器并注入对象工厂和出生点提供器。
        /// </summary>
        /// <param name="factory">负责创建和回收敌人的工厂。</param>
        /// <param name="spawnPositionProvider">负责计算敌人出生位置的服务。</param>
        /// <param name="populationSettings">所有敌人分类共享的存活容量。</param>
        public EnemySpawner(
            EnemyFactory factory,
            ISpawnPositionProvider spawnPositionProvider,
            EnemyPopulationSettingsSO populationSettings,
            IEffectService effectService = null,
            CombatEffectCatalogSO effectCatalog = null,
            IPooledEffectService pooledEffectService = null,
            IPlayerCombatTarget playerTarget = null)
        {
            _factory = factory;
            _spawnPositionProvider = spawnPositionProvider;
            _populationSettings = populationSettings;
            _effectService = effectService;
            _effectCatalog = effectCatalog;
            _pooledEffectService = pooledEffectService;
            _playerTarget = playerTarget;
        }

        /// <summary>
        /// 按生成分类创建敌人，成功后立即更新总数与分类数。
        /// </summary>
        /// <param name="prefab">已配置的怪物预制体。</param>
        /// <param name="category">普通、精英或 Boss 的生成分类。</param>
        /// <param name="healthMultiplier">难度生命倍率。</param>
        /// <param name="speedMultiplier">难度速度倍率。</param>
        /// <param name="initialRotation">本次生成使用的世界初始旋转。</param>
        /// <returns>实际生成成功时返回 true。</returns>
        public bool TrySpawnEnemy(
            Enemy prefab,
            EnemySpawnCategory category,
            float healthMultiplier,
            float speedMultiplier,
            Quaternion initialRotation)
        {
            if (_disposed || prefab == null || category == EnemySpawnCategory.Child ||
                _populationSettings == null || !_populationSettings.HasCapacity(_activeEnemies.Count))
            {
                return false;
            }

            Vector3 spawnPosition = _spawnPositionProvider.GetSpawnPosition();
            Enemy enemy = _factory.CreateEnemy(
                prefab.gameObject,
                category,
                spawnPosition,
                healthMultiplier,
                speedMultiplier,
                initialRotation);
            if (enemy == null)
            {
                return false;
            }

            enemy.OnDied += HandleEnemyDied;
            _activeEnemies.Add(enemy);
            _categoryCounts[(int)category]++;
            _activeEnemyCount.Value = _activeEnemies.Count;
            PlaySpawnEffect(spawnPosition);
            return true;
        }

        /// <summary>子敌人不占普通目标、沿用预制体旋转，但与母体共享总存活上限。</summary>
        public Enemy TrySpawnChild(
            Enemy parent,
            Enemy prefab,
            Vector3 position,
            float healthMultiplier,
            float speedMultiplier)
        {
            if (_disposed || parent == null || prefab == null || _populationSettings == null ||
                !_populationSettings.HasCapacity(_activeEnemies.Count))
            {
                return null;
            }

            Enemy enemy = _factory.CreateEnemy(
                prefab.gameObject,
                EnemySpawnCategory.Child,
                position,
                healthMultiplier,
                speedMultiplier,
                prefab.transform.rotation);
            if (enemy == null)
            {
                return null;
            }

            enemy.OnDied += HandleEnemyDied;
            _activeEnemies.Add(enemy);
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
            _categoryCounts[(int)enemy.SpawnCategory]--;
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
            if (_disposed)
            {
                return;
            }

            UpdateRewardFlights();
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
                StartRewardFlight(enemy);
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

            ClearRewardFlights();
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

        /// <summary>仅为实际到账的伤害击杀启动目录中的奖励粒子。</summary>
        private void StartRewardFlight(Enemy enemy)
        {
            if (enemy == null ||
                enemy.DeathCause != EnemyDeathCause.Damage ||
                !enemy.TryGetComponent(out EnemyDeathRewardComponent reward) ||
                !reward.HasPositiveReward ||
                _effectCatalog == null ||
                _pooledEffectService == null ||
                _playerTarget?.Transform == null ||
                !_effectCatalog.TryGet(EffectId.EnemyReward, out EffectCatalogEntry entry) ||
                entry.Prefab == null)
            {
                return;
            }

            GameObject effect = _pooledEffectService.Play(
                entry.Prefab,
                enemy.transform.position,
                Quaternion.identity,
                Vector3.one * Mathf.Max(0.01f, entry.Scale),
                ManualEffectLifetime);
            if (effect != null)
            {
                _rewardFlights.Add(new RewardFlight(
                    effect,
                    effect.GetComponent<ParticleSystem>(),
                    Mathf.Max(0.01f, entry.TravelSpeed),
                    Mathf.Max(0f, entry.ArrivalDistance)));
            }
        }

        /// <summary>跟随玩家移动奖励粒子，并在抵达、粒子结束或目标失效时归还对象池。</summary>
        private void UpdateRewardFlights()
        {
            Transform target = _playerTarget != null ? _playerTarget.Transform : null;
            for (int index = _rewardFlights.Count - 1; index >= 0; index--)
            {
                RewardFlight flight = _rewardFlights[index];
                if (flight.Effect == null ||
                    target == null ||
                    !target.gameObject.activeInHierarchy ||
                    (flight.Particles != null && !flight.Particles.IsAlive(true)))
                {
                    ReturnRewardFlight(index);
                    continue;
                }

                Transform effectTransform = flight.Effect.transform;
                Vector3 targetPosition = target.position;
                Vector3 nextPosition = Vector3.MoveTowards(
                    effectTransform.position,
                    targetPosition,
                    flight.Speed * Mathf.Max(0f, Time.deltaTime));
                effectTransform.position = nextPosition;

                float arrivalDistanceSquared = flight.ArrivalDistance * flight.ArrivalDistance;
                if ((targetPosition - nextPosition).sqrMagnitude <= arrivalDistanceSquared)
                {
                    ReturnRewardFlight(index);
                }
            }
        }

        /// <summary>停止并归还一个奖励粒子实例。</summary>
        private void ReturnRewardFlight(int index)
        {
            GameObject effect = _rewardFlights[index].Effect;
            if (effect != null)
            {
                _pooledEffectService?.Stop(effect);
            }

            _rewardFlights.RemoveAt(index);
        }

        /// <summary>场景或本局结束时归还所有仍在飞行的奖励粒子。</summary>
        private void ClearRewardFlights()
        {
            for (int index = _rewardFlights.Count - 1; index >= 0; index--)
            {
                ReturnRewardFlight(index);
            }
        }
    }
}
