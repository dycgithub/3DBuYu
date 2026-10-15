using GameSystem;
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
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _difficultyLabel;
        [SerializeField] private TextMeshProUGUI _elapsedTimeLabel;
        [SerializeField] private TextMeshProUGUI _killCountLabel;
        [SerializeField] private TextMeshProUGUI _rewardLabel;
        [SerializeField] private Button _returnButton;

        [Inject] private GameManager _gameManager;
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
        private void HandleSettled(bool isVictory, int _)
        {
            if (_gameManager == null || !HasReferences())
            {
                return;
            }

            ResultOutcome outcome = isVictory ? ResultOutcome.Victory : ResultOutcome.Defeat;
            Refresh(outcome);
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

        /// <summary>更新胜负标题、难度/时间/击杀摘要和实际结算奖励。</summary>
        private void Refresh(ResultOutcome outcome)
        {
            GameSession session = _gameManager.Session;
            DifficultyConfig difficulty = _gameManager.CurrentDifficulty;
            int killCount = session != null ? Mathf.Max(0, session.KillCount) : 0;
            bool isVictory = outcome == ResultOutcome.Victory;
            int reward = isVictory ? Mathf.Max(0, _gameManager.VictoryReward) : 0;

            _titleLabel.text = L10n.Get(isVictory ? "result.victory" : "result.defeat");
            string difficultyName = difficulty != null
                ? L10n.GetOrDefault($"difficulty.{difficulty.DifficultyId}", difficulty.DifficultyId)
                : L10n.Get("result.unknown");
            _difficultyLabel.text = L10n.Get("result.difficulty", difficultyName);
            _elapsedTimeLabel.text = L10n.Get("result.time", FormatTime(session?.ElapsedTime ?? 0f));
            _killCountLabel.text = L10n.Get("result.kills", killCount);
            _rewardLabel.text = isVictory
                ? L10n.Get("result.points", reward)
                : L10n.Get("result.failureReward");
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
            Refresh(_gameManager.CurrentState == GameState.Settled ? ResultOutcome.Victory : ResultOutcome.Defeat);
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
            _titleLabel ??= FindText("TitleLabel");
            _difficultyLabel ??= FindText("DifficultyLabel");
            _elapsedTimeLabel ??= FindText("ElapsedTimeLabel");
            _killCountLabel ??= FindText("KillCountLabel");
            _rewardLabel ??= FindText("RewardLabel");
            _returnButton ??= FindButton("ReturnButton");
        }

        /// <summary>
        /// 检查面板控件和结算服务是否齐全。
        /// </summary>
        private bool HasReferences()
        {
            return _group != null &&
                   _titleLabel != null &&
                   _difficultyLabel != null &&
                   _elapsedTimeLabel != null &&
                   _killCountLabel != null &&
                   _rewardLabel != null &&
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
