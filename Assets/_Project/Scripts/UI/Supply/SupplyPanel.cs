using _Project.UI.Animations;
using UnityEngine;
using UnityEngine.UI;

public class SupplyPanel : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _returnButton;
    [SerializeField] private UIPanelShowHide _panelTween;
    
    // CanvasGroup 必须由场景预置，运行时只读取引用，不补挂组件。
    private CanvasGroup _panelGroup;

    private void Start()
    {
        if (_returnButton != null)
            _returnButton.onClick.AddListener(Hide);
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
            _panelGroup = _panelRoot.GetComponent<CanvasGroup>();
        return _panelGroup;
    }
}
