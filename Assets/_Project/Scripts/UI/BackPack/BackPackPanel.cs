using _Project.UI.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI.BackPack
{
    public class BackPackPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _returnButton;
        [SerializeField] private UIPanelShowHide _panelTween;

        private CanvasGroup _panelGroup;

        private void Start()
        {
            if (_returnButton != null)
                _returnButton.onClick.AddListener(Hide);
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
        
        private CanvasGroup GetPanelGroup()
        {
            if (_panelGroup == null && _panelRoot != null)
                _panelGroup = _panelRoot.GetComponent<CanvasGroup>() ?? _panelRoot.AddComponent<CanvasGroup>();
            return _panelGroup;
        }
    }
}