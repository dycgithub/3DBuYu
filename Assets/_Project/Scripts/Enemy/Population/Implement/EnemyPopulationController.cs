using System.Collections.Generic;
using GameSystem;
using R3;
using Services;
using UnityEngine;
using VContainer;

namespace EnemySystem
{
    /// <summary>管理普通怪物人口和精英、Boss 的一次性时间规则。</summary>
    public sealed class EnemyPopulationController : MonoBehaviour, IEnemyPopulationService
    {
        [Inject] private IEnemySpawner _spawner;
        [Inject] private EnemyPopulationSettingsSO _populationSettings;

        private readonly Subject<Unit> _started = new();
        private readonly Subject<DifficultyConfig> _difficultyChanged = new();
        private DifficultyConfig _difficulty;
        private NormalPopulationSchedule _normalSchedule;
        private SpecialEnemySpawnSchedule _specialSchedule;
        private NormalPopulationSchedule _tickingNormalSchedule;
        private bool _paused;
        private float _elapsedTime;
        private bool _spawnFailureReported;
        private bool _spawnerCleared;
        private bool _destroyed;

        public int EnemiesAlive => _spawner?.ActiveEnemyCount ?? 0;
        public int MaximumPopulation { get; private set; }
        public DifficultyLevel Difficulty { get; private set; }
        public Observable<int> OnEnemiesAliveChanged => _spawner.OnActiveEnemyCountChanged;
        public Observable<Unit> OnPopulationStarted => _started;
        public Observable<DifficultyConfig> OnDifficultyChanged => _difficultyChanged;

        /// <summary>启动当前难度的普通人口和特殊时间表。</summary>
        public void StartPopulation(DifficultyConfig difficulty)
        {
            if (_normalSchedule != null)
            {
                return;
            }

            if (!HasRequiredConfig(difficulty))
            {
                Debug.LogError("[EnemyPopulation] 当前难度没有有效的普通怪物配置或生成服务。", this);
                return;
            }

            _difficulty = difficulty;
            Difficulty = difficulty.Level;
            MaximumPopulation = difficulty.NormalPopulationTarget;
            _normalSchedule = CreateNormalSchedule(difficulty);
            _specialSchedule = new SpecialEnemySpawnSchedule(difficulty.SpecialSpawnRules);
            _elapsedTime = 0f;
            _paused = false;
            _spawnFailureReported = false;
            _spawnerCleared = false;
            _started.OnNext(Unit.Default);
        }

        /// <summary>切换普通人口配置并保留当前已生成的特殊规则状态。</summary>
        public bool TryChangeDifficulty(DifficultyConfig difficulty)
        {
            if (_normalSchedule == null || !HasRequiredConfig(difficulty))
            {
                return false;
            }

            _difficulty = difficulty;
            Difficulty = difficulty.Level;
            MaximumPopulation = difficulty.NormalPopulationTarget;
            _normalSchedule = CreateNormalSchedule(difficulty);
            _specialSchedule.Replace(difficulty.SpecialSpawnRules);
            _spawnFailureReported = false;
            _difficultyChanged.OnNext(difficulty);
            return true;
        }

        /// <summary>按规则 ID 触发一次特殊敌人生成，供未来事件系统调用。</summary>
        public bool TriggerSpecialSpawn(string ruleId)
        {
            if (_paused || _specialSchedule == null)
            {
                return false;
            }

            return _specialSchedule.TryTrigger(ruleId, TrySpawnSpecial);
        }

        /// <summary>推进普通人口补充和特殊时间表。</summary>
        private void Update()
        {
            AdvancePopulation(Time.deltaTime);
        }

        /// <summary>隔离时间步，暂停或停止时不推进任何生成状态。</summary>
        private void AdvancePopulation(float deltaTime)
        {
            if (_paused || _normalSchedule == null || deltaTime <= 0f ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            _elapsedTime += deltaTime;
            NormalPopulationSchedule normalSchedule = _normalSchedule;
            _tickingNormalSchedule = normalSchedule;
            try
            {
                normalSchedule.Tick(
                    deltaTime,
                    () => _spawner.CountCategory(EnemySpawnCategory.Normal),
                    TrySpawnNormal);
            }
            finally
            {
                _tickingNormalSchedule = null;
            }

            if (normalSchedule != _normalSchedule || _specialSchedule == null)
            {
                return;
            }

            SpecialEnemySpawnSchedule specialSchedule = _specialSchedule;
            specialSchedule.Tick(_elapsedTime, TrySpawnSpecial);
        }

        /// <summary>按当前难度权重选择并生成一个普通怪物。</summary>
        private bool TrySpawnNormal()
        {
            if (_normalSchedule == null || _normalSchedule != _tickingNormalSchedule || _paused)
            {
                return false;
            }

            Enemy prefab = NormalEnemySelector.Select(
                _difficulty.NormalSpawnOptions,
                Random.value);
            return TrySpawn(prefab, EnemySpawnCategory.Normal);
        }

        /// <summary>生成时间表指定的一只精英或 Boss。</summary>
        private bool TrySpawnSpecial(SpecialEnemySpawnRule rule)
        {
            if (rule == null || (rule.Category != EnemySpawnCategory.Elite &&
                rule.Category != EnemySpawnCategory.Boss))
            {
                return false;
            }

            return TrySpawn(rule.Prefab, rule.Category);
        }

        /// <summary>满员时等待死亡补位，并为本次人口生成选择随机初始旋转。</summary>
        private bool TrySpawn(Enemy prefab, EnemySpawnCategory category)
        {
            if (_populationSettings != null &&
                !_populationSettings.HasCapacity(_spawner.ActiveEnemyCount))
            {
                return false;
            }

            if (_normalSchedule == null || _paused || prefab == null)
            {
                if (prefab == null && !_spawnFailureReported)
                {
                    _spawnFailureReported = true;
                    Debug.LogError("[EnemyPopulation] 生成规则没有配置有效的敌人预制体。", this);
                }

                return false;
            }

            Quaternion initialRotation = Random.rotation;
            bool spawned = _spawner.TrySpawnEnemy(
                prefab,
                category,
                _difficulty != null ? _difficulty.EnemyHealthMultiplier : 1f,
                _difficulty != null ? _difficulty.EnemySpeedMultiplier : 1f,
                initialRotation);
            if (!spawned && !_spawnFailureReported)
            {
                _spawnFailureReported = true;
                Debug.LogError("[EnemyPopulation] 敌人生成失败，请检查对象池、ECS容量和预制体配置。", this);
            }

            return spawned;
        }

        /// <summary>检查人口服务、目标数量和普通怪物权重配置。</summary>
        private bool HasRequiredConfig(DifficultyConfig difficulty)
        {
            if (_spawner == null || _populationSettings == null || difficulty == null ||
                difficulty.NormalPopulationTarget < 0)
            {
                return false;
            }

            if (difficulty.NormalPopulationTarget == 0)
            {
                return true;
            }

            IReadOnlyList<EnemySpawnOption> options = difficulty.NormalSpawnOptions;
            if (options == null)
            {
                return false;
            }

            for (int index = 0; index < options.Count; index++)
            {
                EnemySpawnOption option = options[index];
                if (option != null && option.Prefab != null && option.Weight > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>按难度资产创建普通人口调度器。</summary>
        /// <param name="difficulty">当前生效的难度配置。</param>
        /// <returns>使用该难度生成参数的调度器。</returns>
        private static NormalPopulationSchedule CreateNormalSchedule(DifficultyConfig difficulty)
        {
            return new NormalPopulationSchedule(
                difficulty.NormalPopulationTarget,
                difficulty.NormalSpawnRatePerSecond,
                difficulty.NormalPopulationRefillRatio);
        }

        /// <summary>暂停普通人口和特殊时间表。</summary>
        public void PausePopulation() => _paused = true;

        /// <summary>恢复暂停前保留的生成计时。</summary>
        public void ResumePopulation() => _paused = false;

        /// <summary>停止时间表并清理当前敌人。</summary>
        public void StopPopulation()
        {
            _normalSchedule = null;
            _specialSchedule = null;
            _tickingNormalSchedule = null;
            _elapsedTime = 0f;
            _paused = false;
            ClearSpawner();
        }

        /// <summary>组件被禁用或场景卸载时停止调度并清理敌人。</summary>
        private void OnDisable()
        {
            StopPopulation();
        }

        /// <summary>停止人口调度、清理敌人并释放控制器持有的事件流。</summary>
        private void OnDestroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;
            StopPopulation();
            _started.Dispose();
            _difficultyChanged.Dispose();
        }

        /// <summary>只执行一次人口清场，避免停止和销毁流程重复归还敌人。</summary>
        private void ClearSpawner()
        {
            if (_spawnerCleared)
            {
                return;
            }

            _spawnerCleared = true;
            _spawner?.ClearAll();
        }
    }
}
