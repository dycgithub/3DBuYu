using System;
using Services;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

/// <summary>
/// 管理当前场景的网格悬停、拖拽和选中状态。
/// 物品的实际放置仍由 GridVM 负责；本类只协调输入状态和选中样式。
/// </summary>
public class InventoryManager : IInventoryDragState, IInventorySelectionState, ITickable
{
    /// <summary>当前鼠标悬停的网格(null=没有)。</summary>
    public GridView HoveredGrid { get; private set; }

    /// <summary>当前拖拽中的物品(null=没有)。</summary>
    public ItemView DraggingItem { get; private set; }

    /// <summary>当前选中的物品(null=没有)。</summary>
    public ItemView SelectedItem { get; private set; }

    /// <summary>选中状态变化事件；参数为新的物品视图，清除时为 null。</summary>
    public event Action<ItemView> SelectionChanged;

    public void SetHoveredGrid(GridView grid) => HoveredGrid = grid;
    public void ClearHoveredGrid() => HoveredGrid = null;
    public void SetDragging(ItemView item) => DraggingItem = item;

    /// <summary>选中物品并切换其轮廓样式。</summary>
    public void Select(ItemView item)
    {
        if (item == null)
            return;

        if (SelectedItem == item)
        {
            item.SetSelected(true);
            return;
        }

        SelectedItem?.SetSelected(false);
        SelectedItem = item;
        SelectedItem.SetSelected(true);
        SelectionChanged?.Invoke(SelectedItem);
    }

    /// <summary>清除当前选中物品并恢复普通轮廓样式。</summary>
    public void ClearSelection()
    {
        if (SelectedItem == null)
            return;

        ItemView previous = SelectedItem;
        SelectedItem = null;
        previous.SetSelected(false);
        SelectionChanged?.Invoke(null);
    }

    public void Tick()
    {
        bool rotatePressed = (Keyboard.current != null && Keyboard.current[Key.R].wasPressedThisFrame)
                             || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);

        if (rotatePressed && DraggingItem != null)
            DraggingItem.Rotate();

        // 拖拽中允许鼠标暂时离开网格；拖拽结束后才按网格悬停状态清除选中。
        if (SelectedItem != null && DraggingItem == null && HoveredGrid == null)
            ClearSelection();
    }
}
