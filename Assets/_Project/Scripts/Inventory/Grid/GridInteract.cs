using Services;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

/// <summary>
/// 跟踪鼠标所在的网格，并处理网格空白区域的取消选择。
/// 子物体上的事件会通知此组件，组件挂在网格根物体即可。
/// </summary>
[RequireComponent(typeof(GridView))]
public class GridInteract : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private GridView _gridView;

    [Inject] private IInventoryDragState _inventory;
    [Inject] private IInventorySelectionState _selection;

    private void Awake()
    {
        _gridView = GetComponent<GridView>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _inventory?.SetHoveredGrid(_gridView);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _inventory?.ClearHoveredGrid();
    }

    /// <summary>点击网格背景时取消当前 Item 选择。</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerPress != null &&
            eventData.pointerPress.GetComponentInParent<ItemView>() != null)
            return;

        _selection?.ClearSelection();
    }
}
