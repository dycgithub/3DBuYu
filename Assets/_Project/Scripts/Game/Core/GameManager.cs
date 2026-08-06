using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer;
using Services;
using InventorySystem;

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

    public class GameManager : MonoBehaviour, Services.IGameEventService
    {
        [Header("关卡(单局)配置")]
        [SerializeField] private StageConfig stageConfig;
        [SerializeField] private BattlePassConfig battlePassConfig;
        [SerializeField] private PlayerLevelConfig playerLevelConfig;

        [Header("系统引用")]
        [Inject] private ResourceManager resourceManager;

        [Header("玩家")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform playerSpawnPoint;

        private readonly GameStateMachine _stateMachine = new();
        private readonly GameSession _session = new();

        [Inject] private TimeManager _timeManager;
        [Inject] private TurretSystem.Turret _turret;
        [Inject] private ITimeRewardSource _rewardSource;
        [Inject] private IWaveEventService _waveService;
        [Inject] private DurabilityManager _durabilityManager;
        [Inject] private IInputService _input;
        [Inject] private PlayerStorage _storage;
        [Inject] private TurretSystem.PlayerLoadout _loadout;
        [Inject] private ItemSystem.Functions.SkillManager _skillManager;

        private PlayerLevelManager _playerLevelManager;
        private BattlePassManager _battlePassManager;
        private GameObject _playerInstance;
        private IDisposable _waveEndedSub;
        private IDisposable _allWavesCompletedSub;

        public GameState CurrentState => _stateMachine.CurrentState;
        public int SessionPoints => _session.SessionPoints;

        public TimeManager Timer => _timeManager;
        public PlayerLevelManager PlayerLevel => _playerLevelManager;
        public BattlePassManager BattlePass => _battlePassManager;
        public GameObject PlayerInstance => _playerInstance;
        public GameSession Session => _session;
        public GameStateMachine StateMachine => _stateMachine;
        public IWaveEventService WaveService => _waveService;
        public ItemSystem.Functions.SkillManager SkillManager => _skillManager;
        public int VictoryReward => stageConfig != null ? stageConfig.victoryRewardPoints : 0;

        public event Action<GameState, GameState> OnGameStateChanged;
        public event Action OnGameStarted;
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

        private void SubscribeInput()
        {
            if (_input != null)
                _input.PausePressed += TogglePause;
        }

        private void UnsubscribeInput()
        {
            if (_input != null)
                _input.PausePressed -= TogglePause;
        }

        private void OnDestroy()
        {
            _stateMachine.OnStateChanged -= HandleStateChanged;

            if (_playerLevelManager != null)
                SaveSystem.SavePlayerLevelData(_playerLevelManager.GetSaveData());
            if (_battlePassManager != null)
                SaveSystem.SaveBattlePassData(_battlePassManager.GetSaveData());

            _waveEndedSub?.Dispose();
            _allWavesCompletedSub?.Dispose();
            SaveInventory();
        }

        private void OnApplicationQuit()
        {
            SaveInventory();
        }

        private void SaveInventory()
        {
            _storage?.Save();
            _loadout?.Save();
        }

        /// <summary>
        /// 装备结算:战斗结束时将炮塔/炮口装备按原价折算为积分并清空装备格。
        /// 由 SceneLoader 返回基地前调用。
        /// 售价由商店经济配置(ShopConfig)负责。
        /// </summary>
        public int SettleEquipment()
        {
            if (_loadout == null) return 0;

            var shop = ProjectLifetimeScope.Instance?.Container?.Resolve<InventorySystem.Shop.ShopConfig>();
            int total = 0;

            foreach (var p in _loadout.TurretInventory.Grid.ToSaveData())
            {
                var config = _loadout.TurretInventory.GetItemConfig(p.instanceId);
                if (config != null) total += shop != null ? shop.GetBasePrice(config.itemId) : 0;
            }

            for (int i = 0; i < _loadout.PortInventories.Count; i++)
            {
                var inv = _loadout.PortInventories[i];
                foreach (var p in inv.Grid.ToSaveData())
                {
                    var config = inv.GetItemConfig(p.instanceId);
                    if (config != null) total += shop != null ? shop.GetBasePrice(config.itemId) : 0;
                }
            }

            if (total > 0 && resourceManager != null)
                resourceManager.AddPoints(total, "装备结算");

            _loadout.ClearEquipment();

            Debug.Log($"[GameManager] 装备结算: {total} 积分");
            return total;
        }

        private void Update()
        {
            if (_stateMachine.CurrentState == GameState.Playing && _timeManager != null)
            {
                _timeManager.Tick(Time.deltaTime);
            }
        }

        public void StartLevel()
        {
            if (stageConfig == null)
            {
                Debug.LogError("[GameManager] stageConfig 未配置。");
                return;
            }

            SubscribeInput();

            _waveEndedSub?.Dispose();
            _allWavesCompletedSub?.Dispose();
            if (_waveService != null)
            {
                _waveEndedSub = _waveService.OnWaveEnded.Subscribe(_ => SaveInventory());
                _allWavesCompletedSub = _waveService.OnAllWavesCompleted.Subscribe(_ => Settle());
            }

            _session.Reset();
            _timeManager.Initialize(stageConfig.timeLimit);
            _timeManager.OnTimeExpired += HandleTimeExpired;

            // 击杀加时配置(每点积分换算秒数)
            if (_rewardSource is KillTimeRewardSource killSource)
                killSource.SetSecondsPerPoint(stageConfig.killTimeRewardPerPoint);

            _skillManager?.Rebuild();

            _stateMachine.ChangeState(GameState.Playing);
            SpawnPlayer();

            // 启动第一波(由 IWaveEventService 决定后续波次如何推进)
            if (_waveService == null)
                Debug.LogError("[GameManager] IWaveEventService 注入失败,无法启动波次");
            _waveService?.StartNextWave();

            OnGameStarted?.Invoke();
            Debug.Log($"[GameManager] 关卡开始: 目标时长 {stageConfig.timeLimit} 秒");
        }

        public void TogglePause()
        {
            if (_stateMachine.CurrentState == GameState.Playing)
            {
                _stateMachine.ChangeState(GameState.Paused);
                _timeManager.Pause();
                _waveService?.PauseWaves();
            }
            else if (_stateMachine.CurrentState == GameState.Paused)
            {
                _stateMachine.ChangeState(GameState.Playing);
                _timeManager.Resume();
                _waveService?.ResumeWaves();
            }
        }

        public void Settle()
        {
            _stateMachine.ChangeState(GameState.Settled);
            _waveService?.StopWaves();

            int reward = stageConfig != null ? stageConfig.victoryRewardPoints : 0;
            if (reward > 0 && resourceManager != null)
                resourceManager.AddPoints(reward, "胜利奖励");

            OnSettled?.Invoke(true, _session.SessionPoints);
            Debug.Log($"[GameManager] 结算完成(胜利): 本局积分 {_session.SessionPoints}, 奖励 {reward} 积分");
        }

        public void GameOver()
        {
            if (_stateMachine.CurrentState == GameState.Failed) return;
            _stateMachine.ChangeState(GameState.Failed);
            _waveService?.StopWaves();
            OnSettled?.Invoke(false, _session.SessionPoints);
            Debug.Log("[GameManager] 游戏失败");
        }

        public void ReturnToMenu()
        {
            _timeManager.OnTimeExpired -= HandleTimeExpired;
            UnsubscribeInput();
            CleanupGame();
            _stateMachine.ChangeState(GameState.Menu);
        }

        private void HandleStateChanged(GameState oldState, GameState newState)
        {
            OnGameStateChanged?.Invoke(oldState, newState);
            _durabilityManager?.SetPaused(newState != GameState.Playing);

            // 状态变化兜底:确保波次在非 Playing 状态下停止
            if (newState == GameState.Settled || newState == GameState.Failed || newState == GameState.Menu)
            {
                _waveService?.StopWaves();
            }
        }

        private void HandleTimeExpired()
        {
            // 纯生存制:自然倒计时耗尽(撑过目标时长)= 胜利;时间被惩罚扣光 = 失败
            if (_timeManager != null && _timeManager.ExpiredByPenalty)
                GameOver();
            else
                Settle();
        }

        private void SpawnPlayer()
        {
            if (_turret != null)
            {
                _playerInstance = _turret.gameObject;
                _playerInstance.tag = "Player";
                return;
            }

            var existingTurret = FindFirstObjectByType<TurretSystem.Turret>();
            if (existingTurret != null)
            {
                _playerInstance = existingTurret.gameObject;
                _playerInstance.tag = "Player";
                return;
            }

            if (playerPrefab == null) return;
            Vector3 pos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
            _playerInstance = Instantiate(playerPrefab, pos, Quaternion.identity);
            _playerInstance.tag = "Player";
        }

        public void OnEnemyKilled(int pointsValue)
        {
            _session.AddPoints(pointsValue);
            resourceManager?.AddPoints(pointsValue, "击杀敌人");
            _playerLevelManager?.AddLifetimePoints(pointsValue);
            _enemyKilled?.Invoke(pointsValue);

            if (_rewardSource != null)
            {
                float timeReward = _rewardSource.GetKillTimeReward(pointsValue, 0);
                if (timeReward > 0f)
                    _timeManager.AddTime(timeReward);
            }
        }

        /// <summary>
        /// 主动技能触发入口(命令模式 Invoker 出口,供 UI/输入/编辑器调试调用)。
        /// </summary>
        public bool TryExecuteSkill(int slotIndex)
        {
            bool executed = _skillManager != null && _skillManager.Execute(slotIndex);
            if (executed)
                Debug.Log($"[GameManager] 释放技能: 槽位 {slotIndex}");
            return executed;
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
