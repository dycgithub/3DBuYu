using System;
using System.Collections.Generic;
using _Project.UI.Shop;
using Interfaces;
using InventorySystem;
using InventorySystem.GridSnap;
using InventorySystem.Shop;
using Services;
using R3;
using UnityEngine;
using VContainer;
using NotificationKind = Services.NotificationKind;

namespace _Project.UI.Inventory
{
    /// <summary>
    /// 集中式拖拽会话(中介者):持有载荷、按下偏移、吸附会话与幽灵。
    /// 吸附由 GridSnapSession 计算,R3 流驱动幽灵定位/着色。
    /// 放置裁决统一走 IPlacementService(同网格移动 / 跨网格转移)。
    /// </summary>
    public class DragSession
    {
        public static DragSession Instance { get; } = new();

        public DragPayload ActivePayload { get; private set; }

        private InventoryGridView _sourceView;
        private ItemSlotView _sourceItem;
        private ItemGhostView _ghost;
        private GridSnapSession _snap;
        private IDisposable _snapSub;
        private SnapCell _anchorCell;
        private IPlacementService _placement;
        private IUINotificationService _notification;

        private DragSession() { }

        /// <summary>开始拖拽:构建载荷、计算按下偏移、创建幽灵与吸附会话。</summary>
        public void Begin(InventoryGridView view, ItemSlotView item, Vector2 screenPos)
        {
            var grid = view.Inventory.Grid;
            var placed = grid.GetPlacedItem(item.Placed.instanceId);
            var config = grid.GetItemConfig(item.Placed.instanceId);
            if (!placed.HasValue || config?.shape == null) return;

            ActivePayload = new DragPayload
            {
                SourceType = DragSourceType.Inventory,
                SourceInventory = view.Inventory,
                InstanceId = placed.Value.instanceId,
                ItemConfig = config,
                Rotation = placed.Value.rotation,
                Cells = InventoryGrid.GetRotatedCells(config.shape, placed.Value.rotation)
            };

            _sourceView = view;
            _sourceItem = item;
            _placement ??= ResolvePlacementService();
            _notification ??= ResolveNotificationService();

            // 按下偏移:鼠标相对物品 (0,0) 格的格子偏移(拖拽中保持鼠标指向同一格)
            var pointerOffset = ComputePointerOffset(view, screenPos, placed.Value);

            _sourceItem.SetDragging(true);

            // 幽灵挂到根 Canvas 最顶层
            var canvas = view.RootCanvas;
            if (canvas == null)
            {
                End();
                return;
            }

            _ghost = UnityEngine.Object.Instantiate(view.GhostPrefab, canvas.transform);
            _ghost.Show(config, ActivePayload.Rotation, view.CellSize, view.Spacing);

            var snapConfig = new GridSnapConfig(view.Step, grid.Width, grid.Height);
            var placement = new InventoryGridPlacement(grid, config, ActivePayload.Rotation, ActivePayload.InstanceId);

            _snap = new GridSnapSession(view.Container, null);
            _snapSub = _snap.SnapChanged.Subscribe(state => OnSnapChanged(state));
            _snap.Begin(snapConfig, placement, ToSnapCells(ActivePayload.Cells), pointerOffset);
            _snap.UpdateSnap(screenPos);
        }

        public void Update(Vector2 screenPos)
        {
            if (ActivePayload == null || _snap == null) return;

            if (Input.GetKeyDown(KeyCode.R))
                Rotate();

            _snap.UpdateSnap(screenPos);
        }

        /// <summary>落点裁决:目标网格 = 释放所在网格,位置 = 会话锚定格。</summary>
        public void DropOn(InventoryGridView targetView)
        {
            if (ActivePayload == null || _snap == null) return;

            var result = _placement != null
                ? _placement.TryPlace(ActivePayload, targetView.Inventory, _anchorCell.Row, _anchorCell.Col)
                : PlacementResult.TransferFailed;

            if (result != PlacementResult.Success)
                _notification?.ShowToast(GetErrorMessage(result), NotificationKind.Error);

            End();
        }

        /// <summary>商店货架拖拽(ShopItemCellDragHandler):购买/货架内移动由 ShopManager 统一裁决。</summary>
        public void DropShopOn(InventoryGridView targetView, ShopItemCellDragHandler drag)
        {
            if (drag == null || drag.SourceStock == null) return;
            var shop = drag.SourceStock.Owner;
            if (shop == null) return;

            // 货架内移动:免费
            if (ReferenceEquals(drag.SourceStock, targetView.Inventory))
            {
                bool moved = shop.MoveOnStock(drag.ShopInstanceId, drag.Row, drag.Col, drag.Rotation);
                if (!moved)
                    _notification?.ShowToast(ShopManager.GetErrorMessage(PurchaseResult.ShapeBlocked), NotificationKind.Error);
                return;
            }

            // 购买:ShopManager 裁决(存在/可放/扣积分/落网格/移除货架)
            var result = shop.TryPurchaseToSlot(drag.ShopInstanceId, targetView.Inventory.Grid, drag.Row, drag.Col, drag.Rotation);
            if (result != PurchaseResult.Success)
                _notification?.ShowToast(ShopManager.GetErrorMessage(result ?? PurchaseResult.TransferFailed), NotificationKind.Error);
        }

        public void End()
        {
            if (_sourceItem != null)
                _sourceItem.SetDragging(false);
            if (_ghost != null)
                UnityEngine.Object.Destroy(_ghost.gameObject);

            _snapSub?.Dispose();
            _snap?.Dispose();

            _snapSub = null;
            _snap = null;
            _ghost = null;
            ActivePayload = null;
            _sourceView = null;
            _sourceItem = null;
        }

        private void Rotate()
        {
            if (ActivePayload?.ItemConfig?.shape == null || _ghost == null) return;

            ActivePayload.Rotation = (ActivePayload.Rotation + 1) % 4;
            ActivePayload.Cells = InventoryGrid.GetRotatedCells(ActivePayload.ItemConfig.shape, ActivePayload.Rotation);

            // 旋转后按下偏移失效,重置为 (0,0)(鼠标改指物品 (0,0) 格)
            _snap.SetCells(ToSnapCells(ActivePayload.Cells), default);
            _ghost.Show(ActivePayload.ItemConfig, ActivePayload.Rotation, _sourceView.CellSize, _sourceView.Spacing);
        }

        private void OnSnapChanged(GridSnapState state)
        {
            _anchorCell = state.AnchorCell;

            if (_ghost == null || _sourceView == null) return;
            _ghost.SetValid(state.IsValid);
            _ghost.SnapTo(_sourceView, state.AnchorCell.Row, state.AnchorCell.Col);
        }

        /// <summary>计算按下时鼠标在物品内的格子偏移。</summary>
        private SnapCell ComputePointerOffset(InventoryGridView view, Vector2 screenPos, PlacedItem placed)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    view.Container, screenPos, null, out var local))
            {
                return default;
            }

            // 物品 (0,0) 格左上角在容器本地坐标(视觉坐标,Y 向下为正)
            Vector2 origin = new Vector2(placed.col * view.Step, -placed.row * view.Step);
            int offsetCol = Mathf.RoundToInt((local.x - origin.x) / view.Step);
            int offsetRow = Mathf.RoundToInt(-(local.y - origin.y) / view.Step);
            return new SnapCell(offsetRow, offsetCol);
        }

        private static IPlacementService ResolvePlacementService()
            => UnityEngine.Object.FindFirstObjectByType<ProjectLifetimeScope>()?.Container?.Resolve<IPlacementService>();

        private static IUINotificationService ResolveNotificationService()
            => UnityEngine.Object.FindFirstObjectByType<ProjectLifetimeScope>()?.Container?.Resolve<IUINotificationService>();

        private static string GetErrorMessage(PlacementResult result) => result switch
        {
            PlacementResult.ShapeBlocked => "这里放不下！",
            PlacementResult.TypeNotAllowed => "该物品不能放入此装备位！",
            PlacementResult.TransferFailed => "放置失败",
            PlacementResult.NoTarget => "目标背包不可用",
            _ => "无法放置",
        };

        private static List<SnapCell> ToSnapCells(List<(int row, int col)> cells)
        {
            var result = new List<SnapCell>(cells.Count);
            foreach (var (r, c) in cells)
                result.Add(new SnapCell(r, c));
            return result;
        }
    }
}
