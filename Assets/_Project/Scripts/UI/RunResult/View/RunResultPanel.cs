using GameSystem;
using Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace _Project.UI.RunResult.View
{
    /// <summary>
    /// 使用当前语言显示单局结算，语言刷新不重新结算或重置返回状态。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RunResultPanel : MonoBehaviour
    {
        private enum ResultOutcome
        {
            Victory,
            Defeat,
        }

        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TextMeshProUGUI _eyebrowLabel;
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _subtitleLabel;
        [SerializeField] private TextMeshProUGUI _difficultyLabel;
        [SerializeField] private TextMeshProUGUI _elapsedTimeLabel;
        [SerializeField] private TextMeshProUGUI _killCountLabel;
        [SerializeField] private TextMeshProUGUI _bestStreakLabel;
        [SerializeField] private TextMeshProUGUI _sessionPointsLabel;
        [SerializeField] private TextMeshProUGUI _settlementMultiplierLabel;
        [SerializeField] private TextMeshProUGUI _settlementFormulaLabel;
        [SerializeField] private TextMeshProUGUI _failureRewardLabel;
        [SerializeField] private GameObject _victoryContent;
        [SerializeField] private GameObject _failureContent;
        [SerializeField] private Button _returnButton;

        [Inject] private GameManager _gameManager;
        [Inject] private IKillStreakService _killStreak;
        [Inject] private SceneLoader _sceneLoader;

        private bool _eventsSubscribed;
        private bool _isReturning;

        /// <summary>
        /// 订阅结算事件并处理场景已经处于结算状态的情况。
        /// </summary>
        private void Start()
        {
            ResolveReferences();
            _returnButton?.onClick.AddListener(ReturnToBase);
            LocalizationSettings.SelectedLocaleChanged += RefreshLanguage;

            if (!HasReferences())
            {
                Debug.LogWarning("[RunResultPanel] 结算面板缺少必要控件。", this);
                return;
            }

            _gameManager.OnSettled += HandleSettled;
            _gameManager.OnGameStarted += HandleStarted;
            _eventsSubscribed = true;
            HidePanel();

            if (_gameManager.CurrentState is GameState.Settled or GameState.Failed)
            {
                HandleSettled(
                    _gameManager.CurrentState == GameState.Settled,
                    _gameManager.SessionPoints);
            }
        }

        /// <summary>
        /// 解除事件和按钮监听，避免场景卸载后访问旧对象。
        /// </summary>
        private void OnDestroy()
        {
            _returnButton?.onClick.RemoveListener(ReturnToBase);
            LocalizationSettings.SelectedLocaleChanged -= RefreshLanguage;

            if (_eventsSubscribed && _gameManager != null)
            {
                _gameManager.OnSettled -= HandleSettled;
                _gameManager.OnGameStarted -= HandleStarted;
            }
        }

        /// <summary>
        /// 从 GameManager 结算事件刷新面板。
        /// </summary>
        private void HandleSettled(bool isVictory, int sessionPoints)
        {
            if (_gameManager == null || !HasReferences())
            {
                return;
            }

            ResultOutcome outcome = isVictory ? ResultOutcome.Victory : ResultOutcome.Defeat;
            Refresh(outcome, sessionPoints);
            ShowPanel();
        }

        /// <summary>
        /// 新一局开始时隐藏上一局结算。
        /// </summary>
        private void HandleStarted()
        {
            _isReturning = false;
            HidePanel();
        }

        /// <summary>
        /// 根据胜负结果更新全部结算文本和内容区。
        /// </summary>
        private void Refresh(ResultOutcome outcome, int sessionPoints)
        {
            GameSession session = _gameManager.Session;
            DifficultyConfig difficulty = _gameManager.CurrentDifficulty;
            int normalizedPoints = Mathf.Max(0, sessionPoints);
            int killCount = session != null ? Mathf.Max(0, session.KillCount) : 0;
            int bestStreak = _killStreak != null
                ? Mathf.Max(0, _killStreak.BestStreak.CurrentValue)
                : 0;
            int basePoints = Mathf.Max(0, _gameManager.BaseSessionPoints);
            int overtimePoints = Mathf.Max(0, _gameManager.OvertimeSessionPoints);
            float multiplier = _gameManager.SettlementMultiplier;
            float overtimeMultiplier = Mathf.Max(1f, _gameManager.OvertimeMultiplier);
            bool isVictory = outcome == ResultOutcome.Victory;
            int reward = isVictory ? Mathf.Max(0, _gameManager.VictoryReward) : 0;

            _eyebrowLabel.text = L10n.Get("ui.runComplete");
            _titleLabel.text = L10n.Get(isVictory ? "result.victory" : "result.defeat");
            _subtitleLabel.text = L10n.Get(isVictory ? "result.victorySubtitle" : "result.defeatSubtitle");
            string difficultyName = difficulty != null
                ? L10n.GetOrDefault($"difficulty.{difficulty.DifficultyId}", difficulty.DifficultyId)
                : L10n.Get("result.unknown");
            _difficultyLabel.text = L10n.Get("result.difficulty", difficultyName);
            _elapsedTimeLabel.text = L10n.Get("result.time", FormatTime(session?.ElapsedTime ?? 0f));
            _killCountLabel.text = L10n.Get("result.kills", killCount);
            _bestStreakLabel.text = L10n.Get("result.streak", bestStreak);
            _sessionPointsLabel.text =
                L10n.Get("result.points", basePoints, overtimePoints, normalizedPoints);
            _settlementMultiplierLabel.text =
                L10n.Get("result.multiplier", multiplier, overtimeMultiplier);
            _settlementFormulaLabel.text =
                $"floor({basePoints} × {multiplier:0.##} + " +
                $"{overtimePoints} × {overtimeMultiplier:0.##}) = {reward}";
            _failureRewardLabel.text = L10n.Get("result.failureReward");

            _victoryContent.SetActive(isVictory);
            _failureContent.SetActive(!isVictory);
            _failureRewardLabel.gameObject.SetActive(!isVictory);
            _isReturning = false;
            _returnButton.interactable = true;
        }

        /// <summary>只更新已结算战局的译文，并保留正在返回的按钮状态。</summary>
        private void RefreshLanguage(Locale _)
        {
            if (_gameManager == null || !HasReferences() ||
                _gameManager.CurrentState is not (GameState.Settled or GameState.Failed))
            {
                return;
            }

            bool returning = _isReturning;
            Refresh(_gameManager.CurrentState == GameState.Settled ? ResultOutcome.Victory : ResultOutcome.Defeat,
                _gameManager.SessionPoints);
            _isReturning = returning;
            _returnButton.interactable = !returning;
        }

        /// <summary>
        /// 防止重复点击并清理战斗状态后返回基地。
        /// </summary>
        private void ReturnToBase()
        {
            if (_isReturning)
            {
                return;
            }

            if (_sceneLoader == null)
            {
                Debug.LogError("[RunResultPanel] 未注入 SceneLoader，无法返回基地。", this);
                return;
            }

            _isReturning = true;
            _returnButton.interactable = false;
            HidePanel();
            _gameManager?.ReturnToMenu();
            _sceneLoader.ReturnToMainMenu();
        }

        /// <summary>
        /// 显示面板并恢复按钮输入。
        /// </summary>
        private void ShowPanel()
        {
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }

        /// <summary>
        /// 隐藏面板并释放战斗场景输入。
        /// </summary>
        private void HidePanel()
        {
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            if (_returnButton != null)
            {
                _returnButton.interactable = false;
            }
        }

        /// <summary>
        /// 绑定场景中的 UGUI 控件；序列化引用为空时按名称补齐。
        /// </summary>
        private void ResolveReferences()
        {
            _group ??= GetComponent<CanvasGroup>();
            _eyebrowLabel ??= FindText("EyebrowLabel");
            _titleLabel ??= FindText("TitleLabel");
            _subtitleLabel ??= FindText("SubtitleLabel");
            _difficultyLabel ??= FindText("DifficultyLabel");
            _elapsedTimeLabel ??= FindText("ElapsedTimeLabel");
            _killCountLabel ??= FindText("KillCountLabel");
            _bestStreakLabel ??= FindText("BestStreakLabel");
            _sessionPointsLabel ??= FindText("SessionPointsLabel");
            _settlementMultiplierLabel ??= FindText("SettlementMultiplierLabel");
            _settlementFormulaLabel ??= FindText("SettlementFormulaLabel");
            _failureRewardLabel ??= FindText("FailureRewardLabel");
            _victoryContent ??= FindObject("VictoryContent");
            _failureContent ??= FindObject("FailureContent");
            _returnButton ??= FindButton("ReturnButton");
        }

        /// <summary>
        /// 检查面板控件和结算服务是否齐全。
        /// </summary>
        private bool HasReferences()
        {
            return _group != null &&
                   _eyebrowLabel != null &&
                   _titleLabel != null &&
                   _subtitleLabel != null &&
                   _difficultyLabel != null &&
                   _elapsedTimeLabel != null &&
                   _killCountLabel != null &&
                   _bestStreakLabel != null &&
                   _sessionPointsLabel != null &&
                   _settlementMultiplierLabel != null &&
                   _settlementFormulaLabel != null &&
                   _failureRewardLabel != null &&
                   _victoryContent != null &&
                   _failureContent != null &&
                   _returnButton != null &&
                   _gameManager != null;
        }

        /// <summary>
        /// 按名称查找结算面板内的 TMP 文本。
        /// </summary>
        private TextMeshProUGUI FindText(string objectName)
        {
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int index = 0; index < labels.Length; index++)
            {
                if (labels[index].name == objectName)
                {
                    return labels[index];
                }
            }

            return null;
        }

        /// <summary>
        /// 按名称查找结算面板内的对象。
        /// </summary>
        private GameObject FindObject(string objectName)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == objectName)
                {
                    return transforms[index].gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// 按名称查找结算面板内的按钮。
        /// </summary>
        private Button FindButton(string objectName)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                if (buttons[index].name == objectName)
                {
                    return buttons[index];
                }
            }

            return null;
        }

        /// <summary>
        /// 将秒数格式化为分钟和秒。
        /// </summary>
        private static string FormatTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                seconds = 0f;
            }

            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
        }
    }
}
