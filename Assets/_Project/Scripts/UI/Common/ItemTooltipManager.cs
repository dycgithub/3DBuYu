using System;
using Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.UI.Common
{
    /// <summary>场景内 Item Tooltip 的协调服务。</summary>
    public enum ItemTooltipMode
    {
        /// <summary>由选中状态驱动固定位置的完整 Tooltip。</summary>
        Fixed,

        /// <summary>由鼠标悬浮驱动跟随指针的简洁 Tooltip。</summary>
        Moving,
    }

    /// <summary>
    /// 管理当前场景的 Tooltip 生命周期、内容刷新和独立开关。
    /// UIScene 和 GameScene 各自注册一个实例，避免跨场景持有已经销毁的 UI 引用。
    /// </summary>
    public sealed class ItemTooltipManager : IItemTooltipService, IDisposable
    {
        private readonly IInventorySelectionState _selection;
        private readonly IInventoryDragState _drag;
        private readonly IShopService _shop;
        private readonly ItemTooltipMode _mode;

        private ItemTooltip _selectedTooltip;
        private ItemTooltip _hoverTooltip;
        private ItemView _hoverItem;
        private bool _hoverEnabled = true;
        private bool _selectedEnabled = true;

        /// <summary>当前是否启用鼠标悬浮 Tooltip。</summary>
        public bool HoverTooltipEnabled => _hoverEnabled;

        /// <summary>当前是否启用选中 Tooltip。</summary>
        public bool SelectedTooltipEnabled => _selectedEnabled;

        public ItemTooltipManager(
            IInventorySelectionState selection,
            IInventoryDragState drag,
            IShopService shop,
            ItemTooltipMode mode,
            bool hoverEnabled,
            bool selectedEnabled)
        {
            _selection = selection;
            _drag = drag;
            _shop = shop;
            _mode = mode;
            _hoverEnabled = hoverEnabled;
            _selectedEnabled = selectedEnabled;
            _selection.SelectionChanged += OnSelectionChanged;
            HideExistingFixedPanel();
        }

        /// <inheritdoc />
        public void ShowHover(ItemView item, Vector2 screenPosition)
        {
            if (_mode != ItemTooltipMode.Moving || !_hoverEnabled || item == null ||
                item.ItemVM?.Definition == null || _drag.DraggingItem != null)
            {
                if (_hoverItem == item)
                    HideHoverInternal();
                return;
            }

            _hoverItem = item;
            _hoverTooltip = EnsureHoverTooltip(item);
            if (_hoverTooltip == null)
                return;

            _hoverTooltip.Show(BuildContent(item));
            _hoverTooltip.PositionNearScreen(screenPosition);
        }

        /// <inheritdoc />
        public void MoveHover(ItemView item, Vector2 screenPosition)
        {
            if (_mode != ItemTooltipMode.Moving || !_hoverEnabled || item == null)
                return;

            if (_hoverItem != item)
            {
                ShowHover(item, screenPosition);
                return;
            }

            _hoverTooltip?.PositionNearScreen(screenPosition);
        }

        /// <inheritdoc />
        public void HideHover(ItemView item)
        {
            if (_hoverItem != item)
                return;

            HideHoverInternal();
        }

        /// <inheritdoc />
        public void RefreshSelectedTooltip()
        {
            if (_mode != ItemTooltipMode.Fixed)
                return;

            ItemView selected = _selection.SelectedItem;
            if (!_selectedEnabled || selected == null || selected.ItemVM?.Definition == null)
            {
                _selectedTooltip?.Hide();
                return;
            }

            _selectedTooltip = EnsureSelectedTooltip(selected);
            if (_selectedTooltip != null)
                _selectedTooltip.Show(BuildContent(selected));
        }

        /// <inheritdoc />
        public void SetHoverTooltipEnabled(bool enabled)
        {
            _hoverEnabled = enabled;
            if (!enabled)
            {
                HideHoverInternal();
                return;
            }

            if (_hoverItem != null && _drag.DraggingItem == null)
            {
                Vector2 position = Mouse.current != null
                    ? Mouse.current.position.ReadValue()
                    : Vector2.zero;
                ShowHover(_hoverItem, position);
            }
        }

        /// <inheritdoc />
        public void SetSelectedTooltipEnabled(bool enabled)
        {
            _selectedEnabled = enabled;
            if (!enabled)
            {
                _selectedTooltip?.Hide();
                return;
            }

            RefreshSelectedTooltip();
        }

        /// <summary>释放事件订阅并隐藏场景内 Tooltip。</summary>
        public void Dispose()
        {
            _selection.SelectionChanged -= OnSelectionChanged;
            _selectedTooltip?.Hide();
            _hoverTooltip?.Hide();
            _selectedTooltip = null;
            _hoverTooltip = null;
            _hoverItem = null;
        }

        private void OnSelectionChanged(ItemView item)
        {
            if (_mode != ItemTooltipMode.Fixed)
                return;

            RefreshSelectedTooltip();
        }

        private void HideExistingFixedPanel()
        {
            if (_mode != ItemTooltipMode.Fixed)
                return;

            GameObject panel = GameObject.Find("ItemInfoPanel");
            if (panel == null)
                return;

            _selectedTooltip = panel.GetComponent<ItemTooltip>();
            if (_selectedTooltip == null)
                return;

            _selectedTooltip.Initialize(true);
            _selectedTooltip.Hide();
        }

        private ItemTooltip EnsureSelectedTooltip(ItemView source)
        {
            Canvas canvas = FindCanvas(source);
            if (canvas == null)
                return null;

            if (_selectedTooltip != null && _selectedTooltip.gameObject != null)
                return _selectedTooltip;

            Transform panel = FindChild(canvas.transform, "ItemInfoPanel");
            if (panel == null)
                return null;

            _selectedTooltip = panel.GetComponent<ItemTooltip>();
            if (_selectedTooltip == null)
                return null;

            panel.SetAsLastSibling();
            _selectedTooltip.Initialize(true);
            return _selectedTooltip;
        }

        private ItemTooltip EnsureHoverTooltip(ItemView source)
        {
            Canvas canvas = FindCanvas(source);
            if (canvas == null)
                return null;

            if (_hoverTooltip != null && _hoverTooltip.gameObject != null)
                return _hoverTooltip;

            Transform panel = FindChild(canvas.transform, "ItemHoverTooltip");
            if (panel == null)
                return null;

            _hoverTooltip = panel.GetComponent<ItemTooltip>();
            if (_hoverTooltip == null)
                return null;

            _hoverTooltip.Initialize(false);
            return _hoverTooltip;
        }

        private ItemTooltipContent BuildContent(ItemView item)
        {
            int price = item.ItemVM.Definition.Price;
            if (_shop != null)
                price = _shop.GetPrice(item.ItemVM);

            GridType gridType = item.OwnerGrid != null
                ? item.OwnerGrid.GridType
                : GridType.StorageForShop;
            return ItemTooltipTextBuilder.Build(item.ItemVM, gridType, price);
        }

        private static Canvas FindCanvas(ItemView source)
            => source != null ? source.GetComponentInParent<Canvas>() : null;

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null)
                return null;

            RectTransform[] children = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == childName)
                    return children[i];
            }

            return null;
        }

        private void HideHoverInternal()
        {
            _hoverTooltip?.Hide();
            _hoverItem = null;
        }
    }
}
