using GameSystem;
using R3;
using Services;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

/// <summary>
/// 按需显示本局积分、当前连杀和本局总击杀的 UI Toolkit HUD。
/// UXML 负责布局，服务和 GameManager 提供权威数据，本组件只负责计时、计数反馈、显隐和生命周期。
/// </summary>
public sealed partial class GameintStatisticsUIToolkit : MonoBehaviour
{
    private const string HiddenClass = "is-hidden";
    private const string ImpactClass = "is-impact";
    private const float PanelVisibleDuration = 1.2f;
    private const float PanelEntryDuration = 0.14f;
    private const float CardEntryDuration = 0.12f;
    private const float StepInterval = 0.035f;
    private const float FastestStepInterval = 0.006f;
    private const int FastStepThreshold = 40;
    private const int MaxStepsPerFrame = 128;
    private const float ImpactDuration = 0.22f;
    private const float ImpactFlashDuration = 0.12f;
    private const float StepPunchDuration = 0.1f;
    private const float StepLiftDuration = 0.1f;
    private const float StepLift = 4f;
    private const float MaximumMagnitudeScale = 1.24f;
    private const float MagnitudeScalePerDigit = 0.06f;
    private const float PointsPunch = 0.08f;
    private const float StreakPunch = 0.18f;
    private const float KillsPunch = 0.12f;

    private struct CounterMotion
    {
        public int Value;
        public int Target;
        public int RenderedValue;
        public float StepTimer;
        public float PunchTime;
        public float PunchDuration;
        public float FlashTime;
        public float LiftTime;
        public float EntryTime;
    }

    [Header("显示")]
    [SerializeField, Tooltip("是否显示战斗数字面板；关闭后只更新数字，顶部计时仍显示。")]
    private bool _showPanel = true;
    [SerializeField] private UIDocument _uiDocument;

    [Inject] private GameManager _gameManager;
    [Inject] private IGameEventService _gameEvents;
    [Inject] private IKillStreakService _killStreak;

    private readonly CompositeDisposable _subscriptions = new();
    private VisualElement _panel;
    private VisualElement _pointsCard;
    private VisualElement _streakCard;
    private VisualElement _killsCard;
    private Label _remainingTimeLabel;
    private Label _pointsLabel;
    private Label _streakLabel;
    private Label _killsLabel;
    private CounterMotion _pointsMotion;
    private CounterMotion _streakMotion;
    private CounterMotion _killsMotion;
    private float _hideTimer;
    private float _panelEntryTime;
    private int _displayedTimeSeconds = int.MinValue;
    private bool _displayedOvertime;
    private bool _initialized;

    /// <summary>
    /// 获取 UXML 视觉树，初始化隐藏状态并建立战斗数据订阅。
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

        BindValues(_uiDocument.rootVisualElement);
        if (!HasAllValues())
        {
            Debug.LogWarning("[GameintStatisticsUIToolkit] UXML 缺少计时或战斗数字控件。", this);
            return;
        }

        _initialized = true;
        RefreshAll();
        Subscribe();
    }

    /// <summary>
    /// 在运行时修改 Inspector 开关时立即清理或保留 HUD 的按需显示能力。
    /// </summary>
    private void OnValidate()
    {
        ApplyVisibility();
    }

    /// <summary>
    /// 释放 R3 订阅和战斗事件，避免场景卸载后继续访问视觉树。
    /// </summary>
    private void OnDestroy()
    {
        _initialized = false;
        _subscriptions.Dispose();

        if (_gameEvents != null)
        {
            _gameEvents.EnemyKilled -= HandleEnemyKilled;
        }

        if (_gameManager != null)
        {
            _gameManager.OnGameStarted -= HandleGameStarted;
            _gameManager.OnRunTimeChanged -= HandleRunTimeChanged;
            _gameManager.OnSettled -= HandleSettled;
        }
    }

    /// <summary>
    /// 缓存 UXML 中的计时 Label、面板、卡片和三个数字 Label。
    /// </summary>
    private void BindValues(VisualElement root)
    {
        _panel = root.Q<VisualElement>("statistics-panel");
        _pointsCard = root.Q<VisualElement>("points-card");
        _streakCard = root.Q<VisualElement>("streak-card");
        _killsCard = root.Q<VisualElement>("kills-card");
        _remainingTimeLabel = root.Q<Label>("remaining-time-value");
        _pointsLabel = root.Q<Label>("points-value");
        _streakLabel = root.Q<Label>("streak-value");
        _killsLabel = root.Q<Label>("kills-value");
    }

    /// <summary>
    /// 订阅当前连杀状态、击杀事件、结算事件和新局事件。
    /// </summary>
    private void Subscribe()
    {
        if (_killStreak != null)
        {
            _subscriptions.Add(_killStreak.CurrentStreak.Subscribe(HandleStreakChanged));
        }

        if (_gameEvents != null)
        {
            _gameEvents.EnemyKilled += HandleEnemyKilled;
        }

        if (_gameManager != null)
        {
            _gameManager.OnGameStarted += HandleGameStarted;
            _gameManager.OnRunTimeChanged += HandleRunTimeChanged;
            _gameManager.OnSettled += HandleSettled;
        }
    }

    /// <summary>
    /// 读取计时和战斗统计的权威状态；开局或新局切换不会强行弹出数字面板。
    /// </summary>
    private void RefreshAll()
    {
        int points = _gameManager?.Session != null
            ? Mathf.Max(0, _gameManager.Session.SessionPoints)
            : 0;
        int kills = _gameManager?.Session != null
            ? Mathf.Max(0, _gameManager.Session.KillCount)
            : 0;
        int streak = _killStreak != null
            ? Mathf.Max(0, _killStreak.CurrentStreak.CurrentValue)
            : 0;

        SetImmediate(ref _pointsMotion, points);
        SetImmediate(ref _streakMotion, streak);
        SetImmediate(ref _killsMotion, kills);
        RefreshTime(_gameManager?.Session != null ? _gameManager.Session.ElapsedTime : 0f);
        HidePanel();
    }

    /// <summary>按当前战局阶段更新剩余时间或超时累计时间。</summary>
    private void HandleRunTimeChanged(float elapsedTime)
    {
        RefreshTime(elapsedTime);
    }

    /// <summary>倒计时向上取整，超时计时向下取整，并仅在秒数变化时刷新文本。</summary>
    private void RefreshTime(float elapsedTime)
    {
        if (_remainingTimeLabel == null)
        {
            return;
        }

        bool isOvertime = _gameManager != null && _gameManager.IsOvertime;
        float seconds = isOvertime
            ? _gameManager.OvertimeElapsedTime
            : Mathf.Max(0f, (_gameManager != null ? _gameManager.TargetDuration : 0f) - elapsedTime);
        int totalSeconds = isOvertime
            ? Mathf.FloorToInt(Mathf.Max(0f, seconds))
            : Mathf.CeilToInt(seconds);
        if (_displayedTimeSeconds == totalSeconds && _displayedOvertime == isOvertime)
        {
            return;
        }

        _displayedTimeSeconds = totalSeconds;
        _displayedOvertime = isOvertime;
        int minutes = totalSeconds / 60;
        int remainder = totalSeconds % 60;
        string prefix = isOvertime ? "+" : string.Empty;
        _remainingTimeLabel.text = $"{prefix}{minutes:D2}:{remainder:D2}";
    }

    /// <summary>
    /// 击杀事件发生在 GameManager 写入本局会话数据之后，更新积分和总击杀目标。
    /// </summary>
    private void RefreshAfterKill()
    {
        int points = _gameManager?.Session != null
            ? Mathf.Max(0, _gameManager.Session.SessionPoints)
            : 0;
        int kills = _gameManager?.Session != null
            ? Mathf.Max(0, _gameManager.Session.KillCount)
            : 0;

        UpdateCounter(ref _pointsMotion, points, _pointsLabel, _pointsCard);
        UpdateCounter(ref _killsMotion, kills, _killsLabel, _killsCard);
    }

    /// <summary>
    /// 将当前连杀目标设置为服务推送的值，并只显示发生变化的连杀卡片。
    /// </summary>
    private void HandleStreakChanged(int value)
    {
        UpdateCounter(ref _streakMotion, value, _streakLabel, _streakCard);
    }

    /// <summary>
    /// 响应一次有效击杀，推进积分和本局总击杀的逐点动画。
    /// </summary>
    private void HandleEnemyKilled(int _)
    {
        RefreshAfterKill();
    }

    /// <summary>
    /// 结算前未派发击杀事件时，补读最后一次本局积分和击杀变化。
    /// </summary>
    private void HandleSettled(bool _, int __)
    {
        RefreshAfterKill();
    }

    /// <summary>
    /// 新局开始时清除上一局的数字、计时和卡片可见状态。
    /// </summary>
    private void HandleGameStarted()
    {
        RefreshAll();
    }

    /// <summary>
    /// 更新单个计数目标；数据变化时显示对应卡片，否则保持隐藏。
    /// </summary>
    private void UpdateCounter(
        ref CounterMotion motion,
        int value,
        Label valueLabel,
        VisualElement card)
    {
        if (!SetTarget(ref motion, value))
        {
            return;
        }

        if (!_showPanel)
        {
            SetImmediate(ref motion, motion.Target);
            RenderCounter(ref motion, valueLabel);
            ResetVisual(valueLabel, card);
            return;
        }

        RevealCounter(ref motion, card);
        RevealPanel();
    }

    /// <summary>
    /// 按 Inspector 开关关闭 HUD；重新开启后等待下一次数值变化再显示。
    /// </summary>
    private void ApplyVisibility()
    {
        if (!_initialized || _panel == null || _showPanel)
        {
            return;
        }

        HidePanel();
    }

    /// <summary>
    /// 显示发生变化的卡片，并在首次出现时播放短暂入场缩放。
    /// </summary>
    private static void RevealCounter(ref CounterMotion motion, VisualElement card)
    {
        bool wasHidden = card.ClassListContains(HiddenClass);
        card.EnableInClassList(HiddenClass, false);
        if (wasHidden)
        {
            motion.EntryTime = CardEntryDuration;
        }
    }

    /// <summary>
    /// 显示整个面板并重置自动隐藏计时。
    /// </summary>
    private void RevealPanel()
    {
        if (!_showPanel)
        {
            return;
        }

        bool wasHidden = _panel.ClassListContains(HiddenClass);
        _panel.EnableInClassList(HiddenClass, false);
        if (wasHidden)
        {
            _panelEntryTime = PanelEntryDuration;
            _panel.style.scale = new Scale(Vector3.one * 0.88f);
            _panel.style.opacity = 0f;
        }

        _hideTimer = PanelVisibleDuration;
    }

    /// <summary>
    /// 将计数器直接设置为目标值，用于初始化、隐藏和新局重置。
    /// </summary>
    private static void SetImmediate(ref CounterMotion motion, int value)
    {
        int normalized = Mathf.Max(0, value);
        motion.Value = normalized;
        motion.Target = normalized;
        motion.RenderedValue = int.MinValue;
        motion.StepTimer = 0f;
        motion.PunchTime = 0f;
        motion.PunchDuration = 0f;
        motion.FlashTime = 0f;
        motion.LiftTime = 0f;
        motion.EntryTime = 0f;
    }

    /// <summary>
    /// 设置计数器目标；新目标到来时从当前数字继续，不排队堆积旧动画。
    /// </summary>
    private static bool SetTarget(ref CounterMotion motion, int value)
    {
        int normalized = Mathf.Max(0, value);
        if (motion.Target == normalized)
        {
            return false;
        }

        bool willIncrease = normalized > motion.Value;
        motion.Target = normalized;
        motion.StepTimer = 0f;
        if (willIncrease)
        {
            motion.PunchTime = ImpactDuration;
            motion.PunchDuration = ImpactDuration;
            motion.FlashTime = ImpactFlashDuration;
        }
        else
        {
            motion.PunchTime = 0f;
            motion.PunchDuration = 0f;
            motion.FlashTime = 0f;
        }

        return true;
    }

    /// <summary>
    /// 按未缩放时间推进面板、逐点数字和自动隐藏计时。
    /// </summary>
    private void Update()
    {
        if (!_initialized || !_showPanel)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime <= 0f || !IsFinite(deltaTime))
        {
            return;
        }

        TickPanel(deltaTime);
        bool stepped = TickCounter(
            ref _pointsMotion,
            _pointsLabel,
            _pointsCard,
            PointsPunch,
            deltaTime);
        stepped |= TickCounter(
            ref _streakMotion,
            _streakLabel,
            _streakCard,
            StreakPunch,
            deltaTime);
        stepped |= TickCounter(
            ref _killsMotion,
            _killsLabel,
            _killsCard,
            KillsPunch,
            deltaTime);

        if (stepped)
        {
            _hideTimer = PanelVisibleDuration;
        }
        else
        {
            _hideTimer = Mathf.Max(0f, _hideTimer - deltaTime);
        }

        if (_hideTimer <= 0f && !HasActiveMotion())
        {
            HidePanel();
        }
    }

    /// <summary>检查 UXML 是否包含计时、面板、三张卡片和三个数字 Label。</summary>
    private bool HasAllValues()
    {
        return _panel != null &&
               _pointsCard != null &&
               _streakCard != null &&
               _killsCard != null &&
               _remainingTimeLabel != null &&
               _pointsLabel != null &&
               _streakLabel != null &&
               _killsLabel != null;
    }
}
