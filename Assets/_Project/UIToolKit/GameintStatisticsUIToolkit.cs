using System;
using GameSystem;
using R3;
using Services;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

/// <summary>
/// 显示 GameScene 本局战斗统计的 UI Toolkit 视图。
/// 视觉树由 UXML 配置，运行时只负责绑定战斗服务并更新 Label。
/// </summary>
public sealed class GameintStatisticsUIToolkit : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    [Inject] private GameManager _gameManager;
    [Inject] private IGameEventService _gameEvents;
    [Inject] private IKillStreakService _killStreak;
    [Inject] private IEnemyPopulationService _population;
    [Inject] private IPointsService _points;
    private readonly CompositeDisposable _subscriptions = new();
    private Label _difficultyLabel;
    private Label _currentStreakLabel;
    private Label _bestStreakLabel;
    private Label _totalKillsLabel;
    private Label _enemyCountLabel;
    private Label _globalPointsLabel;
    private Label _elapsedTimeLabel;
    private Label _victoryRewardLabel;

    /// <summary>
    /// 获取 UIDocument 的视觉树并建立所有战斗状态订阅。
    /// </summary>
    private void Start()
    {
        if (_uiDocument == null)
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        if (_uiDocument == null || _uiDocument.rootVisualElement == null)
        {
            Debug.LogWarning("[GameintStatisticsUIToolkit] 未找到 UIDocument 或视觉树。", this);
            return;
        }

        BindLabels(_uiDocument.rootVisualElement);
        if (!HasAllLabels())
        {
            Debug.LogWarning("[GameintStatisticsUIToolkit] UXML 缺少统计 Label。", this);
            return;
        }

        Subscribe();
        RefreshAll();
    }

    /// <summary>
    /// 释放 R3 订阅和 GameManager 事件，避免场景卸载后继续访问视觉树。
    /// </summary>
    private void OnDestroy()
    {
        _subscriptions.Dispose();

        if (_gameEvents != null)
        {
            _gameEvents.EnemyKilled -= HandleEnemyKilled;
        }

        if (_gameManager != null)
        {
            _gameManager.OnGameStarted -= HandleGameStarted;
            _gameManager.OnDifficultyChanged -= HandleDifficultyChanged;
            _gameManager.OnRunTimeChanged -= HandleRunTimeChanged;
        }
    }

    /// <summary>
    /// 将 UXML 中预先配置的 Label 缓存到视图字段。
    /// </summary>
    private void BindLabels(VisualElement root)
    {
        _difficultyLabel = root.Q<Label>("difficulty-label");
        _currentStreakLabel = root.Q<Label>("current-streak-label");
        _bestStreakLabel = root.Q<Label>("best-streak-label");
        _totalKillsLabel = root.Q<Label>("total-kills-label");
        _enemyCountLabel = root.Q<Label>("enemy-count-label");
        _globalPointsLabel = root.Q<Label>("global-points-label");
        _elapsedTimeLabel = root.Q<Label>("elapsed-time-label");
        _victoryRewardLabel = root.Q<Label>("victory-reward-label");
    }

    /// <summary>
    /// 订阅本局战斗状态的事实流。
    /// </summary>
    private void Subscribe()
    {
        if (_killStreak != null)
        {
            _subscriptions.Add(_killStreak.CurrentStreak.Subscribe(HandleCurrentStreakChanged));
            _subscriptions.Add(_killStreak.BestStreak.Subscribe(HandleBestStreakChanged));
        }

        if (_population != null)
        {
            _subscriptions.Add(_population.OnPopulationStarted.Subscribe(_ => RefreshDifficulty()));
            _subscriptions.Add(_population.OnEnemiesAliveChanged.Subscribe(_ => RefreshEnemyCount()));
        }

        if (_points != null)
        {
            _subscriptions.Add(_points.Points.Subscribe(HandlePointsChanged));
        }

        if (_gameEvents != null)
        {
            _gameEvents.EnemyKilled += HandleEnemyKilled;
        }

        if (_gameManager != null)
        {
            _gameManager.OnGameStarted += HandleGameStarted;
            _gameManager.OnDifficultyChanged += HandleDifficultyChanged;
            _gameManager.OnRunTimeChanged += HandleRunTimeChanged;
        }
    }

    /// <summary>
    /// 刷新所有当前值，覆盖视图晚于战斗服务启动的情况。
    /// </summary>
    private void RefreshAll()
    {
        RefreshDifficulty();
        RefreshEnemyCount();
        RefreshTotalKills();
        RefreshGlobalPoints();
        RefreshElapsedTime();
        RefreshVictoryReward();
    }

    private void HandleCurrentStreakChanged(int value)
    {
        _currentStreakLabel.text = $"当前连杀：{Mathf.Max(0, value)}";
    }

    private void HandleBestStreakChanged(int value)
    {
        _bestStreakLabel.text = $"最高连杀：{Mathf.Max(0, value)}";
    }

    private void HandlePointsChanged(int value)
    {
        RefreshGlobalPoints(value);
        RefreshVictoryReward();
    }

    private void HandleElapsedTimeChanged(float value)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(value));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        _elapsedTimeLabel.text = $"经过时间：{minutes:D2}:{seconds:D2}";
    }

    private void RefreshElapsedTime()
    {
        if (_gameManager?.Session != null)
        {
            HandleElapsedTimeChanged(_gameManager.Session.ElapsedTime);
        }
    }

    private void HandleEnemyKilled(int _)
    {
        RefreshTotalKills();
        RefreshVictoryReward();
    }

    private void HandleRunTimeChanged(float elapsedTime)
    {
        HandleElapsedTimeChanged(elapsedTime);
        RefreshVictoryReward();
    }

    private void HandleGameStarted()
    {
        RefreshAll();
    }

    private void HandleDifficultyChanged(DifficultyConfig _)
    {
        RefreshDifficulty();
        RefreshVictoryReward();
    }

    private void RefreshDifficulty()
    {
        if (_population != null)
        {
            _difficultyLabel.text = $"难度：{FormatDifficulty(_population.Difficulty)}";
        }
    }

    private void RefreshEnemyCount()
    {
        int alive = _population != null ? Mathf.Max(0, _population.EnemiesAlive) : 0;
        _enemyCountLabel.text = $"当前敌人：{alive}";
    }

    private void RefreshTotalKills()
    {
        int kills = _gameManager?.Session != null ? _gameManager.Session.KillCount : 0;
        _totalKillsLabel.text = $"本局总击杀：{Mathf.Max(0, kills)}";
    }

    private void RefreshVictoryReward()
    {
        int sessionPoints = _gameManager != null ? Mathf.Max(0, _gameManager.SessionPoints) : 0;
        float settlementMultiplier = _gameManager != null ? _gameManager.SettlementMultiplier : 1f;
        int reward = _gameManager != null ? Mathf.Max(0, _gameManager.VictoryRewardPreview) : 0;
        _victoryRewardLabel.text =
            $"胜利奖励：floor(本局积分 {sessionPoints} × 结算倍率 {settlementMultiplier:0.##}) = {reward}";
    }

    private void RefreshGlobalPoints()
    {
        if (_points != null)
        {
            RefreshGlobalPoints(_points.Points.CurrentValue);
        }
    }

    private void RefreshGlobalPoints(int value)
    {
        _globalPointsLabel.text = $"全局剩余积分：{Mathf.Max(0, value)}";
    }

    private bool HasAllLabels()
    {
        return _difficultyLabel != null &&
               _currentStreakLabel != null &&
               _bestStreakLabel != null &&
               _totalKillsLabel != null &&
               _enemyCountLabel != null &&
               _globalPointsLabel != null &&
               _elapsedTimeLabel != null &&
               _victoryRewardLabel != null;
    }

    private static string FormatDifficulty(DifficultyLevel level)
    {
        return level switch
        {
            DifficultyLevel.Easy => "1 简单",
            DifficultyLevel.Normal => "2 普通",
            DifficultyLevel.Advanced => "3 进阶",
            DifficultyLevel.Expert => "4 专家",
            DifficultyLevel.Nightmare => "5 噩梦",
            DifficultyLevel.Extreme => "6 究极",
            DifficultyLevel.Impossible => "7 ??通过",
            _ => "未知"
        };
    }
}
