using System;
using UnityEngine;
using VContainer;
using Services;
using CombatSystem;

namespace GameSystem
{
    /// <summary>表示游戏当前所处的单局状态。</summary>
    public enum GameState
    {
        /// <summary>主菜单状态。</summary>
        Menu,

        /// <summary>战斗进行中。</summary>
        Playing,

        /// <summary>战斗胜利并完成结算。</summary>
        Settled,

        /// <summary>战斗失败。</summary>
        Failed,

        /// <summary>战斗暂时暂停。</summary>
        Paused
    }

    /// <summary>统一管理单局状态、经过时间、能量耗尽判定和结算。</summary>
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

        [Inject] private IGamePauseService _pauseService;
        [Inject] private IEnergyService _energy;
        [Inject] private Play.CentralCore centralCore;
        [Inject] private IEnemyPopulationService _populationService;
        [Inject] private RunRuleService _runRuleService;
        [Inject] private IKillStreakService _killStreak;
        [Inject] private IInputService _input;
        [Inject] private CentralSkillLoadout _centralSkillLoadout;
        [Inject] private TransmitterAttackLoadout _transmitterAttackLoadout;
        [Inject] private IInventoryTransferStorage _inventoryTransferStorage;

        private PlayerLevelManager _playerLevelManager;
        private BattlePassManager _battlePassManager;
        private GameObject _playerInstance;
        private int _lastSettlementReward;
        private bool _isStartingLevel;
        private bool _energyDepletedDuringStart;
        private DifficultySelection _difficultySelection;

        /// <summary>获取当前游戏状态。</summary>
        public GameState CurrentState => _stateMachine.CurrentState;
        bool Services.ICombatPhaseService.CanPerformCombatActions
            => _stateMachine.CurrentState == GameState.Playing;

        /// <summary>获取本局累计积分。</summary>
        public int SessionPoints => _session.SessionPoints;

        /// <summary>获取本局能量服务。</summary>
        public IEnergyService Energy => _energy;

        /// <summary>获取玩家等级管理器。</summary>
        public PlayerLevelManager PlayerLevel => _playerLevelManager;

        /// <summary>获取通行证管理器。</summary>
        public BattlePassManager BattlePass => _battlePassManager;

        /// <summary>获取当前生成的玩家对象。</summary>
        public GameObject PlayerInstance => _playerInstance;

        /// <summary>获取本局会话数据。</summary>
        public GameSession Session => _session;

        /// <summary>获取游戏状态机。</summary>
        public GameStateMachine StateMachine => _stateMachine;

        /// <summary>获取数量调度服务。</summary>
        public IEnemyPopulationService PopulationService => _populationService;

        /// <summary>获取本局当前难度配置。</summary>
        public DifficultyConfig CurrentDifficulty
            => _difficultySelection?.Current ?? stageConfig?.defaultDifficulty;

        /// <summary>获取最近一次胜利结算奖励。</summary>
        public int VictoryReward => _lastSettlementReward;

        /// <summary>按当前本局积分和关卡结算倍率计算的胜利奖励预览。</summary>
        public int VictoryRewardPreview => CalculateSettlementReward();

        /// <summary>当前本局使用的结算倍率。</summary>
        public float SettlementMultiplier => CurrentDifficulty != null
            ? CurrentDifficulty.SettlementMultiplier
            : 1f;

        /// <summary>游戏状态发生变化时触发，参数依次为旧状态和新状态。</summary>
        public event Action<GameState, GameState> OnGameStateChanged;

        /// <summary>关卡成功进入战斗状态后触发。</summary>
        public event Action OnGameStarted;

        /// <summary>本局难度成功切换后触发。</summary>
        public event Action<DifficultyConfig> OnDifficultyChanged;

        /// <summary>本局运行时间变化时触发，参数单位为秒。</summary>
        public event Action<float> OnRunTimeChanged;

        /// <summary>本局完成结算时触发，参数依次为是否胜利和本局积分。</summary>
        public event Action<bool, int> OnSettled;

        event Action<int> Services.IGameEventService.EnemyKilled
        {
            add { _enemyKilled += value; }
            remove { _enemyKilled -= value; }
        }

        private event Action<int> _enemyKilled;

        int Services.IGameEventService.NotifyEnemyKilled(int pointsValue) => OnEnemyKilled(pointsValue);

        /// <summary>
        /// 初始化玩家等级、通行证和状态机事件订阅。
        /// </summary>
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

        /// <summary>
        /// 注册本局输入和能量耗尽事件。
        /// </summary>
        private void ActivateRunSubscriptions()
        {
            // 开局可能发生在上一局结算之后；先清理旧订阅，确保每个回调至多注册一次。
            DeactivateRunSubscriptions();

            if (_input != null)
            {
                _input.PausePressed += TogglePause;
            }

            if (_energy != null)
            {
                _energy.EnergyDepleted += HandleEnergyDepleted;
            }
        }

        /// <summary>
        /// 注销本局输入和能量耗尽事件，避免重复订阅。
        /// </summary>
        private void DeactivateRunSubscriptions()
        {
            if (_input != null)
            {
                _input.PausePressed -= TogglePause;
            }

            if (_energy != null)
            {
                _energy.EnergyDepleted -= HandleEnergyDepleted;
            }
        }

        /// <summary>
        /// 注销状态机事件、清理本局状态并保存永久进度。
        /// </summary>
        private void OnDestroy()
        {
            _stateMachine.OnStateChanged -= HandleStateChanged;
            DeactivateRunSubscriptions();
            ClearCombatRunState();

            if (_playerLevelManager != null)
            {
                SaveSystem.SavePlayerLevelData(_playerLevelManager.GetSaveData());
            }

            if (_battlePassManager != null)
            {
                SaveSystem.SaveBattlePassData(_battlePassManager.GetSaveData());
            }
        }

        /// <summary>
        /// 推进进行中的时间、连杀和能量状态。
        /// </summary>
        private void Update()
        {
            if (_stateMachine.CurrentState != GameState.Playing)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            _killStreak?.Tick(deltaTime);

            _session.AdvanceTime(deltaTime);
            OnRunTimeChanged?.Invoke(_session.ElapsedTime);

            if (_energy == null)
            {
                return;
            }

            _energy.Tick(deltaTime, stageConfig != null ? stageConfig.baseEnergyDrainPerSecond : 0f);
        }

        /// <summary>
        /// 启动组件所在对象的默认战斗流程。
        /// </summary>
        private void Start()
        {
            StartLevel();
        }

        /// <summary>
        /// 初始化并开始一局战斗。
        /// </summary>
        /// <remarks>没有关卡配置或当前已经在战斗时不会开始新局。</remarks>
        public void StartLevel()
        {
            if (_stateMachine.CurrentState is GameState.Playing or GameState.Paused)
            {
                return;
            }

            if (stageConfig == null)
            {
                Debug.LogError("[GameManager] stageConfig 未配置。");
                return;
            }

            _pauseService?.Resume();
            _difficultySelection = new DifficultySelection(
                stageConfig.defaultDifficulty,
                stageConfig.AvailableDifficulties);
            DifficultyConfig startingDifficulty = _difficultySelection.Current;
            _session.BeginRun(startingDifficulty?.DifficultyId, 0);
            _lastSettlementReward = 0;
            _killStreak?.BeginRun(stageConfig.killStreakWindowSeconds);
            _centralSkillLoadout?.BeginRun();
            _transmitterAttackLoadout?.BeginRun();
            _isStartingLevel = true;
            _energyDepletedDuringStart = false;
            ActivateRunSubscriptions();
            _energy?.Initialize(stageConfig.initialEnergy, stageConfig.maxEnergy);

            _stateMachine.ChangeState(GameState.Playing);
            SpawnPlayer();

            // 开始本局铺怪，后续补充由独立数量调度负责。
            if (_populationService == null)
            {
                Debug.LogError("[GameManager] IEnemyPopulationService 注入失败,无法启动数量调度");
            }
            _populationService?.StartPopulation(startingDifficulty);

            OnGameStarted?.Invoke();
            OnRunTimeChanged?.Invoke(_session.ElapsedTime);

            _isStartingLevel = false;
            if (_energyDepletedDuringStart)
            {
                _energyDepletedDuringStart = false;
                HandleEnergyDepleted();
            }

            Debug.Log($"[GameManager] 关卡开始: 目标时长 {stageConfig.timeLimit} 秒");
        }

        /// <summary>
        /// 在时间窗口内切换本局难度，并让后续敌人使用新配置。
        /// </summary>
        /// <param name="direction">难度调整方向。</param>
        /// <returns>切换成功时返回 true。</returns>
        public bool TryChangeDifficulty(DifficultyChangeDirection direction)
        {
            if (!CanChangeDifficulty(direction) ||
                !_difficultySelection.TryGetTarget(
                    direction,
                    _session.ElapsedTime,
                    out DifficultyConfig target))
            {
                return false;
            }

            if (!_populationService.TryChangeDifficulty(target))
            {
                return false;
            }

            _difficultySelection.Commit(target);
            _session.ChangeDifficulty(target.DifficultyId);
            OnDifficultyChanged?.Invoke(target);
            return true;
        }

        /// <summary>判断指定方向在当前本局状态下是否仍可调整。</summary>
        /// <param name="direction">难度调整方向。</param>
        /// <returns>存在相邻配置且尚未到达 60 秒时返回 true。</returns>
        public bool CanChangeDifficulty(DifficultyChangeDirection direction)
        {
            return IsDifficultyChangeState() &&
                   _populationService != null &&
                   _difficultySelection != null &&
                   _difficultySelection.TryGetTarget(
                       direction,
                       _session.ElapsedTime,
                       out _);
        }

        /// <summary>
        /// 在进行中和暂停状态之间切换战斗。
        /// </summary>
        public void TogglePause()
        {
            if (_stateMachine.CurrentState == GameState.Playing)
            {
                _stateMachine.ChangeState(GameState.Paused);
                _pauseService?.Pause();
                _populationService?.PausePopulation();
            }
            else if (_stateMachine.CurrentState == GameState.Paused)
            {
                _stateMachine.ChangeState(GameState.Playing);
                _pauseService?.Resume();
                _populationService?.ResumePopulation();
            }
        }

        /// <summary>
        /// 以胜利结束当前战斗并完成结算。
        /// </summary>
        public void Settle()
        {
            if (_stateMachine.CurrentState != GameState.Playing &&
                _stateMachine.CurrentState != GameState.Paused)
            {
                return;
            }

            _lastSettlementReward = CalculateSettlementReward();
            _stateMachine.ChangeState(GameState.Settled);
            _pauseService?.Resume();
            DeactivateRunSubscriptions();
            _populationService?.StopPopulation();

            if (_lastSettlementReward > 0)
            {
                resourceManager?.AddPoints(_lastSettlementReward, "本局胜利结算");
                _playerLevelManager?.AddLifetimePoints(_lastSettlementReward);
            }

            ClearCombatRunState();
            OnSettled?.Invoke(true, _session.SessionPoints);
            Debug.Log(
                $"[GameManager] 结算完成(胜利): 本局积分 {_session.SessionPoints}, " +
                $"奖励 {_lastSettlementReward} 积分");
        }

        /// <summary>
        /// 以失败结束当前战斗。
        /// </summary>
        public void GameOver()
        {
            if (_stateMachine.CurrentState != GameState.Playing &&
                _stateMachine.CurrentState != GameState.Paused)
            {
                return;
            }

            _lastSettlementReward = 0;
            _stateMachine.ChangeState(GameState.Failed);
            _pauseService?.Resume();
            DeactivateRunSubscriptions();
            _populationService?.StopPopulation();
            ClearCombatRunState();
            OnSettled?.Invoke(false, _session.SessionPoints);
            Debug.Log("[GameManager] 游戏失败");
        }

        /// <summary>
        /// 清理当前战斗并将状态切换到主菜单。
        /// </summary>
        public void ReturnToMenu()
        {
            _pauseService?.Resume();
            DeactivateRunSubscriptions();
            CleanupGame();
            _stateMachine.ChangeState(GameState.Menu);
        }

        /// <summary>
        /// 转发状态变化并在离开战斗状态时停止敌群调度。
        /// </summary>
        private void HandleStateChanged(GameState oldState, GameState newState)
        {
            OnGameStateChanged?.Invoke(oldState, newState);

            // 状态变化兜底，确保敌群调度在非 Playing 状态下停止。
            if (newState == GameState.Settled || newState == GameState.Failed || newState == GameState.Menu)
            {
                _populationService?.StopPopulation();
            }
        }

        /// <summary>判断当前是否仍属于可调整难度的本局状态。</summary>
        private bool IsDifficultyChangeState()
        {
            return _stateMachine.CurrentState is GameState.Playing or GameState.Paused;
        }

        /// <summary>
        /// 根据本局规则处理能量耗尽后的胜负判定。
        /// </summary>
        private void HandleEnergyDepleted()
        {
            if (_isStartingLevel)
            {
                _energyDepletedDuringStart = true;
                return;
            }

            if (_stateMachine.CurrentState != GameState.Playing)
            {
                return;
            }

            RunVerdict verdict = _runRuleService.JudgeEnergyDepleted(
                _session.ElapsedTime,
                stageConfig != null ? stageConfig.timeLimit : 0f,
                _session.KillCount,
                stageConfig != null ? stageConfig.targetKillCount : 0);

            if (verdict == RunVerdict.Victory)
            {
                Settle();
            }
            else
            {
                GameOver();
            }
        }

        /// <summary>
        /// 获取场景中注入的核心对象根节点作为本局玩家实例。
        /// </summary>
        private void SpawnPlayer()
        {
            if (centralCore == null)
            {
                Debug.LogError("[GameManager] 场景玩家 Turret 未注入,请确认 GameScene 包含 PlayerContainer。", this);
                return;
            }

            _playerInstance = centralCore.transform.root.gameObject;
            _playerInstance.tag = "Player";
            centralCore.gameObject.tag = "Player";
        }

        /// <summary>
        /// 记录一次敌人击杀并将积分计入本局。
        /// </summary>
        /// <param name="pointsValue">敌人提供的基础积分。</param>
        /// <returns>本局实际计入的积分；非战斗状态下返回 0。</returns>
        public int OnEnemyKilled(int pointsValue)
        {
            if (_stateMachine.CurrentState != GameState.Playing)
            {
                return 0;
            }

            int basePoints = Mathf.Max(0, pointsValue);
            _session.RecordKill(basePoints);
            _killStreak?.RegisterKill();
            _enemyKilled?.Invoke(basePoints);
            return basePoints;
        }

        /// <summary>
        /// 根据本局积分和关卡难度计算胜利结算奖励。
        /// </summary>
        /// <returns>本局应发放的永久积分。</returns>
        private int CalculateSettlementReward()
        {
            return RunRuleMath.CalculateSettlementReward(
                _session.SessionPoints,
                SettlementMultiplier);
        }

        /// <summary>清除由全局容器持有的本局库存和装备状态。</summary>
        private void ClearCombatRunState()
        {
            _inventoryTransferStorage?.Clear();
            _centralSkillLoadout?.Clear();
            _transmitterAttackLoadout?.ClearAllItems();
        }

        /// <summary>
        /// 清理玩家实例和当前战斗资源。
        /// </summary>
        private void CleanupGame()
        {
            ClearCombatRunState();

            if (_playerInstance != null)
            {
                Destroy(_playerInstance);
                _playerInstance = null;
            }
            _populationService?.StopPopulation();
        }
    }
}
