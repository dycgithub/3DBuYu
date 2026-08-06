using UnityEngine;
using R3;
using Services;
using GameSystem;
using VContainer;

namespace EnemySystem.Wave
{
    public enum WaveState
    {
        Idle,
        Preparing,
        Spawning,
        Active,
        Ending,
        WaitingForNextWave,
        Completed
    }

    public class WaveController : MonoBehaviour, IWaveEventService
    {
        [SerializeField] private WaveConfig waveConfig;
        [SerializeField] private bool showDebugInfo = true;

        [Inject] private IEnemySpawner _enemySpawner;

        private readonly Subject<int> _onWaveStarted = new();
        private readonly Subject<int> _onWaveEnded = new();
        private readonly Subject<WaveChangedEvent> _onWaveChanged = new();
        private readonly Subject<Unit> _onAllWavesCompleted = new();

        public Observable<int> OnWaveStarted => _onWaveStarted;
        public Observable<int> OnWaveEnded => _onWaveEnded;
        public Observable<WaveChangedEvent> OnWaveChanged => _onWaveChanged;
        public Observable<int> OnEnemiesAliveChanged => _enemySpawner.OnActiveEnemyCountChanged;
        public Observable<Unit> OnAllWavesCompleted => _onAllWavesCompleted;

        public int CurrentWaveNumber { get; private set; }
        public int TotalWaveCount => waveConfig != null ? waveConfig.GetTotalWaveCount() : 0;
        public int EnemiesAlive => _enemySpawner.ActiveEnemyCount;
        public bool IsWaveActive => _currentState is WaveState.Preparing or WaveState.Spawning or WaveState.Active;
        public bool IsSpawning => _currentState == WaveState.Spawning;

        private WaveState _currentState = WaveState.Idle;
        private WaveData _currentWave;
        private bool _isPaused;

        private float _preparationTimer;
        private float _endingTimer;
        private float _waitTimer;
        private SpawnGroupState[] _spawnGroups;

        private struct SpawnGroupState
        {
            public WaveEnemyInfo Info;
            public int SpawnedCount;
            public float Timer;
        }

        private void Update()
        {
            if (_isPaused) return;

            switch (_currentState)
            {
                case WaveState.Preparing:
                    _preparationTimer -= Time.deltaTime;
                    if (_preparationTimer <= 0f)
                        BeginSpawning();
                    break;

                case WaveState.Spawning:
                    UpdateSpawning(Time.deltaTime);
                    break;

                case WaveState.Active:
                    if (_enemySpawner.ActiveEnemyCount <= 0)
                        BeginEnding();
                    break;

                case WaveState.Ending:
                    _endingTimer -= Time.deltaTime;
                    if (_endingTimer <= 0f)
                        HandleWaveEnded();
                    break;

                case WaveState.WaitingForNextWave:
                    _waitTimer -= Time.deltaTime;
                    if (_waitTimer <= 0f)
                        PrepareNextWave();
                    break;
            }
        }

        public void StartNextWave()
        {
            if (_currentState is WaveState.Preparing or WaveState.Spawning or WaveState.Active)
                return;
            if (waveConfig == null) return;
            if (_isPaused) return;

            PrepareNextWave();
        }

        private void PrepareNextWave()
        {
            CurrentWaveNumber++;
            _currentWave = waveConfig.GetWave(CurrentWaveNumber);
            if (_currentWave == null)
            {
                CurrentWaveNumber--;
                _currentState = WaveState.Completed;
                OnAllWavesCompletedReached();
                return;
            }

            _preparationTimer = _currentWave.preparationTime;
            _currentState = WaveState.Preparing;
            _onWaveChanged.OnNext(new WaveChangedEvent(CurrentWaveNumber, TotalWaveCount));
        }

        private void BeginSpawning()
        {
            _currentState = WaveState.Spawning;
            SetupSpawnGroups();
            _onWaveStarted.OnNext(CurrentWaveNumber);
        }

        private void SetupSpawnGroups()
        {
            var enemies = _currentWave.enemies;
            _spawnGroups = new SpawnGroupState[enemies.Count];
            for (int i = 0; i < enemies.Count; i++)
            {
                var info = enemies[i];
                _spawnGroups[i] = new SpawnGroupState
                {
                    Info = info,
                    SpawnedCount = 0,
                    Timer = info.delayStart
                };
            }
        }

        private void UpdateSpawning(float deltaTime)
        {
            bool allDone = true;

            for (int i = 0; i < _spawnGroups.Length; i++)
            {
                var group = _spawnGroups[i];
                if (group.SpawnedCount >= group.Info.spawnCount)
                    continue;

                allDone = false;
                group.Timer -= deltaTime;

                if (group.Timer <= 0f)
                {
                    _enemySpawner.SpawnEnemy(group.Info, _currentWave);
                    group.SpawnedCount++;
                    group.Timer = group.Info.spawnInterval;
                }

                _spawnGroups[i] = group;
            }

            if (allDone)
                _currentState = WaveState.Active;
        }

        private void BeginEnding()
        {
            _endingTimer = _currentWave != null ? _currentWave.clearDelay : 0f;
            _currentState = WaveState.Ending;
        }

        private void HandleWaveEnded()
        {
            _onWaveEnded.OnNext(CurrentWaveNumber);

            var nextWave = waveConfig != null ? waveConfig.GetWave(CurrentWaveNumber + 1) : null;
            if (nextWave == null)
            {
                _currentState = WaveState.Completed;
                OnAllWavesCompletedReached();
                return;
            }

            if (nextWave.trigger.type == WaveTriggerType.PreviousCleared)
            {
                if (nextWave.trigger.delayAfterPrevious > 0f)
                {
                    _waitTimer = nextWave.trigger.delayAfterPrevious;
                    _currentState = WaveState.WaitingForNextWave;
                }
                else
                {
                    PrepareNextWave();
                }
            }
            else
            {
                _currentState = WaveState.Idle;
            }
        }

        private void OnAllWavesCompletedReached()
        {
            _onAllWavesCompleted.OnNext(Unit.Default);
        }

        public void SkipCurrentWave()
        {
            if (_currentState is WaveState.Idle or WaveState.Completed or WaveState.WaitingForNextWave)
                return;

            _enemySpawner.ClearAll();
            _endingTimer = 0f;
            _currentState = WaveState.Ending;
        }

        public void PauseWaves()
        {
            _isPaused = true;
        }

        public void ResumeWaves()
        {
            _isPaused = false;
        }

        public void StopWaves()
        {
            _isPaused = false;
            _currentState = WaveState.Idle;
            CurrentWaveNumber = 0;
            _currentWave = null;
            _enemySpawner.ClearAll();
        }

        public void ClearAllEnemies()
        {
            _enemySpawner.ClearAll();
        }

        private void OnDrawGizmos()
        {
            if (!showDebugInfo) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}