using GameSystem;
using R3;
using Services;
using UnityEngine;
using VContainer;

namespace EnemySystem
{
    /// <summary>按难度启动数量调度，独立发布首次铺怪和补怪周期事件。</summary>
    public sealed class EnemyPopulationController : MonoBehaviour, IEnemyPopulationService
    {
        [SerializeField] private Enemy normalPrefab;
        [SerializeField] private Enemy elitePrefab;
        [SerializeField] private Enemy bossPrefab;
        [Inject] private IEnemySpawner _spawner;
        private readonly Subject<Unit> _started = new();
        private readonly Subject<Unit> _initialCompleted = new();
        private readonly Subject<Unit> _refillStarted = new();
        private readonly Subject<Unit> _refillCompleted = new();
        private EnemyPopulationSchedule _schedule;
        private EnemyPopulationSchedule _tickingSchedule;
        private DifficultyConfig _difficulty;
        private bool _paused;
        private bool _spawnFailureReported;

        public int EnemiesAlive => _spawner?.ActiveEnemyCount ?? 0;
        public int MaximumPopulation { get; private set; }
        public DifficultyLevel Difficulty { get; private set; }
        public Observable<int> OnEnemiesAliveChanged => _spawner.OnActiveEnemyCountChanged;
        public Observable<Unit> OnPopulationStarted => _started;
        public Observable<Unit> OnInitialCompleted => _initialCompleted;
        public Observable<Unit> OnRefillStarted => _refillStarted;
        public Observable<Unit> OnRefillCompleted => _refillCompleted;

        /// <summary>每局只启动一次，提前验证当前难度实际使用的预制体。</summary>
        public void StartPopulation(DifficultyConfig difficulty)
        {
            if (_schedule != null)
            {
                return;
            }
            EnemyQuota quota = EnemyQuota.ForDifficulty(difficulty != null ? difficulty.Level : DifficultyLevel.Easy);
            if (!HasRequiredPrefabs(quota))
            {
                Debug.LogError("[EnemyPopulation] 生成服务或当前难度的怪物预制体未配置。", this);
                return;
            }
            _difficulty = difficulty;
            Difficulty = difficulty != null ? difficulty.Level : DifficultyLevel.Easy;
            MaximumPopulation = quota.Total;
            _paused = false;
            _spawnFailureReported = false;
            _schedule = new EnemyPopulationSchedule(quota);
            _started.OnNext(Unit.Default);
        }

        /// <summary>
        /// 切换后续生成难度并以当前存活数量作为新调度的起点。
        /// </summary>
        /// <param name="difficulty">目标难度配置。</param>
        /// <returns>目标配置有效且切换成功时返回 true。</returns>
        public bool TryChangeDifficulty(DifficultyConfig difficulty)
        {
            if (_schedule == null || difficulty == null)
            {
                return false;
            }

            EnemyQuota quota = EnemyQuota.ForDifficulty(difficulty.Level);
            if (!HasRequiredPrefabs(quota))
            {
                Debug.LogError("[EnemyPopulation] 目标难度的怪物预制体未配置。", this);
                return false;
            }

            int[] existingCounts =
            {
                _spawner.CountRank(EnemyRank.Normal),
                _spawner.CountRank(EnemyRank.Elite),
                _spawner.CountRank(EnemyRank.Boss)
            };
            _difficulty = difficulty;
            Difficulty = difficulty.Level;
            MaximumPopulation = quota.Total;
            _spawnFailureReported = false;
            _schedule = new EnemyPopulationSchedule(quota, existingCounts);
            return true;
        }

        /// <summary>只推进运行中的调度；事件回调停止本局后不再继续发送旧事件。</summary>
        private void Update()
        {
            AdvancePopulation(Time.deltaTime);
        }

        /// <summary>隔离时间步与 Unity 回调，零时间及暂停不产生补怪周期事件。</summary>
        private void AdvancePopulation(float deltaTime)
        {
            if (_paused || _schedule == null || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }
            EnemyPopulationSchedule current = _schedule;
            bool initial = current.InitialCompleted;
            bool refill = current.IsRefilling;
            int primaryEnemiesAlive = _spawner.CountRank(EnemyRank.Normal) +
                _spawner.CountRank(EnemyRank.Elite) +
                _spawner.CountRank(EnemyRank.Boss);
            bool beginsRefill = initial && !refill &&
                (long)primaryEnemiesAlive * 5 < (long)MaximumPopulation * 4;
            _tickingSchedule = current;
            try
            {
                current.Tick(deltaTime, _spawner.CountRank, TrySpawn);
            }
            finally
            {
                _tickingSchedule = null;
            }
            if (current != _schedule)
            {
                return;
            }
            if (!initial && current.InitialCompleted)
            {
                _initialCompleted.OnNext(Unit.Default);
            }
            if (current != _schedule)
            {
                return;
            }
            if (beginsRefill)
            {
                _refillStarted.OnNext(Unit.Default);
            }
            if (current == _schedule && (refill || beginsRefill) && !current.IsRefilling)
            {
                _refillCompleted.OnNext(Unit.Default);
            }
        }

        /// <summary>同步提交生成，成功后存活数立即更新，不存在未计入预算的排队对象。</summary>
        private bool TrySpawn(EnemyRank rank)
        {
            // 数量事件可能同步停止并重开本局，旧循环不得向新调度继续生成。
            if (_schedule == null || _schedule != _tickingSchedule || _paused)
            {
                return false;
            }
            Enemy prefab = rank switch
            {
                EnemyRank.Elite => elitePrefab,
                EnemyRank.Boss => bossPrefab,
                _ => normalPrefab
            };
            bool spawned = _spawner.TrySpawnEnemy(prefab, rank,
                _difficulty != null ? _difficulty.EnemyHealthMultiplier : 1f,
                _difficulty != null ? _difficulty.EnemySpeedMultiplier : 1f);
            if (!spawned && !_spawnFailureReported)
            {
                _spawnFailureReported = true;
                Debug.LogError("[EnemyPopulation] 生成失败，保留剩余配额等待重试；请检查对象池、ECS 容量和预制体。", this);
            }
            return spawned;
        }

        /// <summary>检查当前配额所需的生成服务和预制体是否齐全。</summary>
        /// <param name="quota">待验证的敌人配额。</param>
        /// <returns>当前配额可以尝试生成时返回 true。</returns>
        private bool HasRequiredPrefabs(EnemyQuota quota)
        {
            return _spawner != null &&
                   normalPrefab != null &&
                   (quota.Elite == 0 || elitePrefab != null) &&
                   (quota.Boss == 0 || bossPrefab != null);
        }

        /// <summary>暂停时不累计任何生成时间。</summary>
        public void PausePopulation() => _paused = true;
        /// <summary>从暂停时保留的计时继续。</summary>
        public void ResumePopulation() => _paused = false;

        /// <summary>取消调度后清理全部敌人，防止清理回调触发补怪。</summary>
        public void StopPopulation()
        {
            _schedule = null;
            _paused = false;
            _spawner?.ClearAll();
        }

        /// <summary>释放由本控制器持有的事件流。</summary>
        private void OnDestroy()
        {
            _started.Dispose();
            _initialCompleted.Dispose();
            _refillStarted.Dispose();
            _refillCompleted.Dispose();
        }
    }
}
