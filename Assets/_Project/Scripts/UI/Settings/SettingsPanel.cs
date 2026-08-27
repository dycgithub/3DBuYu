using _Project.UI.Animations;
using GameSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.Settings
{
    /// <summary>
    /// 设置面板:返回基地(仅战斗场景显示)+ 关闭。
    /// 基地与战斗场景各挂一份实例。
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _returnBaseButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private UIPanelShowHide _panelTween;

        [Inject] private SceneLoader _sceneLoader;

        private CanvasGroup _panelGroup;

        private void Start()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);

            if (_returnBaseButton != null)
            {
                _returnBaseButton.onClick.AddListener(ReturnToBase);
                bool inBattle = SceneManager.GetSceneByName("GameScene").isLoaded;
                _returnBaseButton.gameObject.SetActive(inBattle);
            }
        }

        public void Toggle()
        {
            if (_panelTween != null)
            {
                _panelTween.Toggle();
                return;
            }

            var group = GetPanelGroup();
            if (group == null) return;
            bool visible = group.alpha > 0.01f;
            group.alpha = visible ? 0f : 1f;
            group.blocksRaycasts = !visible;
            group.interactable = !visible;
        }

        private void Hide()
        {
            if (_panelTween != null)
            {
                _panelTween.Hide();
                return;
            }
            var group = GetPanelGroup();
            if (group == null) return;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private void ReturnToBase()
        {
            if (_sceneLoader != null)
            {
                Hide();
                _sceneLoader.ReturnToMainMenu();
            }
        }

        private CanvasGroup GetPanelGroup()
        {
            if (_panelGroup == null && _panelRoot != null)
                _panelGroup = _panelRoot.GetComponent<CanvasGroup>() ?? _panelRoot.AddComponent<CanvasGroup>();
            return _panelGroup;
        }
    }
}


