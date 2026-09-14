using GameSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using _Project.UI.Animations;

namespace _Project.UI.Item
{
    /// <summary>
    /// 战斗结算视图。
    /// 视觉层级、文本、按钮和动画组件全部来自场景或预制体，脚本只负责绑定数据与交互。
    /// </summary>
    public sealed class BattleResultPanel : MonoBehaviour
    {
        [Header("视图引用")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _infoText;
        [SerializeField] private Button _returnButton;
        [SerializeField] private UIPanelShowHide _panelTween;
        [SerializeField] private CanvasGroup _rootGroup;

        [Inject] private GameManager _gameManager;
        [Inject] private SceneLoader _sceneLoader;

        private bool _subscribed;

        private void Awake()
        {
            _rootGroup ??= GetComponent<CanvasGroup>();
            _panelTween ??= GetComponent<UIPanelShowHide>();
        }

        private void Start()
        {
            if (_returnButton != null)
                _returnButton.onClick.AddListener(ReturnToBase);

            if (_gameManager == null)
            {
                Debug.LogWarning("[BattleResultPanel] 未注入 GameManager,无法显示结算。", this);
            }
            else
            {
                _gameManager.OnSettled += HandleSettled;
                _subscribed = true;
            }

            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_subscribed && _gameManager != null)
                _gameManager.OnSettled -= HandleSettled;

            if (_returnButton != null)
                _returnButton.onClick.RemoveListener(ReturnToBase);
        }

        private void HandleSettled(bool success, int sessionPoints)
        {
            if (_titleText == null || _infoText == null)
            {
                Debug.LogWarning("[BattleResultPanel] 未配置结算文本引用。", this);
                return;
            }

            int reward = success && _gameManager != null ? _gameManager.VictoryReward : 0;
            _titleText.text = success ? "胜 利" : "失 败";
            _titleText.color = success
                ? new Color(0.4f, 1f, 0.5f)
                : new Color(1f, 0.4f, 0.4f);
            _infoText.text = success
                ? $"本局击杀积分: {sessionPoints}\n胜利奖励: +{reward} 积分"
                : $"本局击杀积分: {sessionPoints}\n未满足本局条件,无结算奖励";

            SetVisible(true);
        }

        private void ReturnToBase()
        {
            SetVisible(false);
            _sceneLoader?.ReturnToMainMenu();
        }

        private void SetVisible(bool visible)
        {
            if (_panelTween != null)
            {
                if (visible)
                    _panelTween.Show();
                else
                    _panelTween.Hide();
                return;
            }

            if (_rootGroup == null)
                return;

            _rootGroup.alpha = visible ? 1f : 0f;
            _rootGroup.interactable = visible;
            _rootGroup.blocksRaycasts = visible;
        }
    }
}
