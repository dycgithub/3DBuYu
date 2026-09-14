using GameSystem;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.Settings
{
    /// <summary>
    /// 绑定 GameScene 中已有的升降难度按钮。
    /// 按钮只提交难度方向，时间窗口、配置和敌群切换由 GameManager 统一处理。
    /// </summary>
    public sealed class DifficultySelector : MonoBehaviour
    {
        [Header("难度按钮")]
        [SerializeField] private Button _increaseButton;
        [SerializeField] private Button _decreaseButton;

        [Inject] private GameManager _gameManager;
        private bool _eventsSubscribed;

        /// <summary>初始化按钮点击监听。</summary>
        private void Awake()
        {
            _increaseButton?.onClick.AddListener(IncreaseDifficulty);
            _decreaseButton?.onClick.AddListener(DecreaseDifficulty);
        }

        /// <summary>订阅本局状态变化并初始化按钮状态。</summary>
        private void Start()
        {
            if (_gameManager != null)
            {
                _gameManager.OnGameStarted += RefreshButtons;
                _gameManager.OnDifficultyChanged += HandleDifficultyChanged;
                _gameManager.OnRunTimeChanged += HandleRunTimeChanged;
                _gameManager.OnGameStateChanged += HandleGameStateChanged;
                _eventsSubscribed = true;
            }

            RefreshButtons();
        }

        /// <summary>移除按钮监听和 GameManager 事件订阅。</summary>
        private void OnDestroy()
        {
            _increaseButton?.onClick.RemoveListener(IncreaseDifficulty);
            _decreaseButton?.onClick.RemoveListener(DecreaseDifficulty);

            if (_eventsSubscribed && _gameManager != null)
            {
                _gameManager.OnGameStarted -= RefreshButtons;
                _gameManager.OnDifficultyChanged -= HandleDifficultyChanged;
                _gameManager.OnRunTimeChanged -= HandleRunTimeChanged;
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        /// <summary>提交升高难度请求。</summary>
        private void IncreaseDifficulty()
        {
            TryChangeDifficulty(DifficultyChangeDirection.Increase);
        }

        /// <summary>提交降低难度请求。</summary>
        private void DecreaseDifficulty()
        {
            TryChangeDifficulty(DifficultyChangeDirection.Decrease);
        }

        /// <summary>向 GameManager 提交方向并立即同步按钮状态。</summary>
        /// <param name="direction">难度调整方向。</param>
        private void TryChangeDifficulty(DifficultyChangeDirection direction)
        {
            _gameManager?.TryChangeDifficulty(direction);
            RefreshButtons();
        }

        /// <summary>难度切换成功后刷新上下按钮边界状态。</summary>
        /// <param name="_">当前生效的难度配置。</param>
        private void HandleDifficultyChanged(DifficultyConfig _)
        {
            RefreshButtons();
        }

        /// <summary>时间推进后在达到 60 秒时立即置灰按钮。</summary>
        /// <param name="_">本局已运行时间。</param>
        private void HandleRunTimeChanged(float _)
        {
            RefreshButtons();
        }

        /// <summary>离开战斗状态后禁用难度调整。</summary>
        /// <param name="_">旧状态。</param>
        /// <param name="__">新状态。</param>
        private void HandleGameStateChanged(GameState _, GameState __)
        {
            RefreshButtons();
        }

        /// <summary>按当前状态、时间窗口和上下边界设置按钮是否可交互。</summary>
        private void RefreshButtons()
        {
            if (_increaseButton != null)
            {
                _increaseButton.interactable = _gameManager != null &&
                    _gameManager.CanChangeDifficulty(DifficultyChangeDirection.Increase);
            }

            if (_decreaseButton != null)
            {
                _decreaseButton.interactable = _gameManager != null &&
                    _gameManager.CanChangeDifficulty(DifficultyChangeDirection.Decrease);
            }
        }
    }
}
