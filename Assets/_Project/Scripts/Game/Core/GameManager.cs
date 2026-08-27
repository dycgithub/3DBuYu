using System;
using UnityEngine;
using VContainer;
using Services;
using CombatSystem;

namespace GameSystem
{
    public enum GameState
    {
        Menu,
        Playing,
        Settled,
        Failed,
        Paused
    }

    /// <summary>管理单局状态，并向战斗系统提供只读的阶段门禁。</summary>
    public class GameManager : MonoBehaviour, Services.IGameEventService, Services.ICombatPhaseService
    {
        [Header("关卡(单局)配置")]
        [SerializeField] private StageConfig stageConfig;
        [SerializeField] private BattlePassConfig battlePassConfig;
        [SerializeField] private PlayerLevelConfig playerLevelConfig;

        [Header("系统引用")]
        [Inject] private ResourceManager resourceManager;

        private readonly GameStateMachine _stateMachine = new();
        private readonly GameSession _session = new();

        [Inject] private TimeManager _timeManager;
        [Inject] private IGamePauseService _pauseService;
        [Inject] private ICombatEnergyService _energy;
        [Inject] private Play.CentralCore centralCore;
        [Inject] private IWaveEventService _waveService;
        [Inject] private IInputService _input;
        [Inject] private CombatLoadout _combatLoadout;
        [Inject] private IInventoryTransferStorage _inventoryTransferStorage;

        private PlayerLevelManager _playerLevelManager;
        private BattlePassManager _battlePassManager;
        private GameObject _playerInstance;
        private int _lastSettlementReward;

        public GameState CurrentState => _stateMachine.CurrentState;
        bool Services.ICombatPhaseService.CanPerformCombatActions
            => _stateMachine.CurrentState == GameState.Playing;
        public int SessionPoints => _session.SessionPoints;

        public TimeManager Timer => _timeManager;
        public ICombatEnergyService Energy => _energy;
        public PlayerLevelManager PlayerLevel => _playerLevelManager;
        public BattlePassManager BattlePass => _battlePassManager;
        public GameObject PlayerInstance => _playerInstance;
        public GameSession Session => _session;
        public GameStateMachine StateMachine => _stateMachine;
        public IWaveEventService WaveService => _waveService;
        public int VictoryReward => _lastSettlementReward;

        public event Action<GameState, GameState> OnGameStateChanged;
        public event Action OnGameStarted;
        public event Action<float> OnRunTimeChanged;
        public event Action<bool, int> OnSettled;

        event Action<int> Services.IGameEventService.EnemyKilled
        {
            add { _enemyKilled += value; }
            remove { _enemyKilled -= value; }
        }
        private event Action<int> _enemyKilled;

        void Services.IGameEventService.NotifyEnemyKilled(int pointsValue) => OnEnemyKilled(pointsValue);

        private void Awake()
        {
            _playerLevelManager = new PlayerLevelManager();
            _playerLevelManager.Initialize(playerLevelConfig);
            _playerLevelManager.LoadFromSave(SaveSystem.LoadPlayerLevelData());

            _battlePassManager = new BattlePassManager();
            _battlePassManager.Initialize(battlePassConfig);
            _battlePassManager.LoadFromSave(SaveSystem.LoadBattlePassData());

            _stateMachine.OnStateChanged += HandleStateChanged;
        }

        private void ActivateRunSubscriptions()
        {
            // 开局可能发生在上一局结算之后；先清理旧订阅，确保每个回调至多注册一次。
            DeactivateRunSubscriptions();

            if (_input != null)
                _input.PausePressed += TogglePause;

            if (_energy != null)
                _energy.EnergyDepleted += HandleEnergyDepleted;
        }

        private void DeactivateRunSubscriptions()
        {
            if (_input != null)
                _input.PausePressed -= TogglePause;

            if (_energy != null)
                _energy.EnergyDepleted -= HandleEnergyDepleted;
        }

        private void OnDestroy()
        {
            _stateMachine.OnStateChanged -= HandleStateChanged;
            DeactivateRunSubscriptions();

            if (_playerLevelManager != null)
                SaveSystem.SavePlayerLevelData(_playerLevelManager.GetSaveData());
            if (_battlePassManager != null)
                SaveSystem.SaveBattlePassData(_battlePassManager.GetSaveData());
        }


        private void Update()
        {
            if (_stateMachine.CurrentState != GameState.Playing)
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return;

            // TimeManager 的剩余时间仅保留兼容显示；本局终局由能量耗尽决定。
            _timeManager?.Tick(deltaTime);

            float targetDuration = stageConfig != null ? stageConfig.timeLimit : 0f;
            _session.AdvanceTime(deltaTime, targetDuration);
            OnRunTimeChanged?.Invoke(_session.ElapsedTime);

            if (_energy == null)
                return;

            _energy.SetCostMultiplier(_session.OvertimeMultiplier);
            _energy.Tick(deltaTime, stageConfig != null ? stageConfig.baseEnergyDrainPerSecond : 0f);

            // 事件负责即时响应，轮询用于覆盖没有经过服务入口的边界调用。
            if (_energy.IsDepleted)
                HandleEnergyDepleted();
        }

        private void Start()
        {
            StartLevel();
        }

        public void StartLevel()
        {
            if (_stateMachine.CurrentState == GameState.Playing)
                return;

            if (stageConfig == null)
            {
                Debug.LogError("[GameManager] stageConfig 未配置。");
                return;
            }

            _pauseService?.Resume();
            _session.Reset();
            _lastSettlementReward = 0;
            _combatLoadout?.BeginRun();
            _timeManager?.Initialize(stageConfig.timeLimit);
            _energy?.Initialize(stageConfig.initialEnergy, stageConfig.maxEnergy);
            ActivateRunSubscriptions();

            _stateMachine.ChangeState(GameState.Playing);
            SpawnPlayer();

            // 启动第一波(由 IWaveEventService 决定后续波次如何推进)
            if (_waveService == null)
                Debug.LogError("[GameManager] IWaveEventService 注入失败,无法启动波次");
            _waveService?.StartNextWave();

            OnGameStarted?.Invoke();
            OnRunTimeChanged?.Invoke(_session.ElapsedTime);

            // 初始能量为 0 时不等待下一帧，立即按本局规则判定。
            if (_energy != null && _energy.IsDepleted)
                HandleEnergyDepleted();

            Debug.Log($"[GameManager] 关卡开始: 目标时长 {stageConfig.timeLimit} 秒");
        }

        public void TogglePause()
        {
            if (_stateMachine.CurrentState == GameState.Playing)
            {
                _stateMachine.ChangeState(GameState.Paused);
                _pauseService?.Pause();
                _waveService?.PauseWaves();
            }
            else if (_stateMachine.CurrentState == GameState.Paused)
            {
                _stateMachine.ChangeState(GameState.Playing);
                _pauseService?.Resume();
                _waveService?.ResumeWaves();
            }
        }

        public void Settle()
        {
            if (_stateMachine.CurrentState is GameState.Settled or GameState.Failed)
                return;

            _lastSettlementReward = CalculateSettlementReward();
            _stateMachine.ChangeState(GameState.Settled);
            _pauseService?.Resume();
            DeactivateRunSubscriptions();
            _waveService?.StopWaves();

            if (_lastSettlementReward > 0)
            {
                resourceManager?.AddPoints(_lastSettlementReward, "本局胜利结算");
                _playerLevelManager?.AddLifetimePoints(_lastSettlementReward);
            }

            _inventoryTransferStorage?.Clear();
            _combatLoadout?.Clear();
            OnSettled?.Invoke(true, _session.SessionPoints);
            Debug.Log(
                $"[GameManager] 结算完成(胜利): 本局积分 {_session.SessionPoints}, " +
                $"奖励 {_lastSettlementReward} 积分");
        }

        public void GameOver()
        {
            if (_stateMachine.CurrentState is GameState.Settled or GameState.Failed)
                return;

            _lastSettlementReward = 0;
            _stateMachine.ChangeState(GameState.Failed);
            _pauseService?.Resume();
            DeactivateRunSubscriptions();
            _waveService?.StopWaves();
            _inventoryTransferStorage?.Clear();
            _combatLoadout?.Clear();
            OnSettled?.Invoke(false, _session.SessionPoints);
            Debug.Log("[GameManager] 游戏失败");
        }

        public void ReturnToMenu()
        {
            _pauseService?.Resume();
            DeactivateRunSubscriptions();
            CleanupGame();
            _stateMachine.ChangeState(GameState.Menu);
        }

        private void HandleStateChanged(GameState oldState, GameState newState)
        {
            OnGameStateChanged?.Invoke(oldState, newState);

            // 状态变化兜底:确保波次在非 Playing 状态下停止
            if (newState == GameState.Settled || newState == GameState.Failed || newState == GameState.Menu)
            {
                _waveService?.StopWaves();
            }
        }

        private void HandleEnergyDepleted()
        {
            if (_stateMachine.CurrentState != GameState.Playing)
                return;

            bool meetsRequirements = RunRuleMath.MeetsRunRequirements(
                _session.ElapsedTime,
                stageConfig != null ? stageConfig.timeLimit : 0f,
                _session.KillCount,
                stageConfig != null ? stageConfig.targetKillCount : 0);

            if (meetsRequirements)
                Settle();
            else
                GameOver();
        }

        private void SpawnPlayer()
        {
            if (centralCore == null)
            {
                Debug.LogError("[GameManager] 场景玩家 Turret 未注入,请确认 GameScene 包含 PlayerContainer。", this);
                return;
            }

            _playerInstance = centralCore.gameObject;
            _playerInstance.tag = "Player";
        }

        public void OnEnemyKilled(int pointsValue)
        {
            if (_stateMachine.CurrentState != GameState.Playing)
                return;

            int scaledPoints = RunRuleMath.CalculateScaledPoints(
                pointsValue,
                _session.OvertimeMultiplier);
            _session.RecordKill(scaledPoints);
            _enemyKilled?.Invoke(scaledPoints);
        }

        private int CalculateSettlementReward()
        {
            float settlementMultiplier = stageConfig?.defaultDifficulty != null
                ? stageConfig.defaultDifficulty.SettlementMultiplier
                : 1f;
            return RunRuleMath.CalculateSettlementReward(
                _session.SessionPoints,
                settlementMultiplier);
        }

        private void CleanupGame()
        {
            if (_playerInstance != null)
            {
                Destroy(_playerInstance);
                _playerInstance = null;
            }
            _waveService?.StopWaves();
        }
    }
}
