using System.Collections.Generic;
using InventorySystem;
using InventorySystem.GridSnap;
using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Project.UI.Inventory
{
    /// <summary>
    /// 物品视图:一个道具一个 UI 预制体。
    /// 位置/尺寸由网格布局数学计算;是拖拽源,拖拽开始时交给 DragSession。
    /// 图标来自 ItemVisualRegistry(表现与逻辑解耦)。
    /// </summary>
    public class ItemSlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image _icon;

        private InventoryGridView _view;
        private PlacedItem _placed;

        public RectTransform Rect { get; private set; }
        public PlacedItem Placed => _placed;

        private void Awake() => Rect = GetComponent<RectTransform>();

        /// <summary>绑定放置记录:设置图标(表现层)、尺寸(旋转后包围盒)、位置。</summary>
        public void Bind(InventoryGridView view, PlacedItem placed)
        {
            _view = view;
            _placed = placed;

            var config = view.Inventory.GetItemConfig(placed.instanceId);
            if (config != null)
                _icon.sprite = ItemVisualHelper.GetIcon(config.itemId);

            var cells = InventoryGrid.GetRotatedCells(config.shape, placed.rotation);
            var (maxC, maxR) = GetBounds(cells);
            Rect.sizeDelta = GridLayoutMath.ShapeSize(maxR + 1, maxC + 1, view.CellSize, view.Spacing);
            SnapTo(placed.row, placed.col);
        }

        /// <summary>吸附到 (row,col) 格(左上角对齐),并置顶保证渲染于格子之上。</summary>
        public void SnapTo(int row, int col)
        {
            Rect.anchoredPosition = GridLayoutMath.CellLocalPos(row, col, _view.Step);
            Rect.SetAsLastSibling();
        }

        /// <summary>拖拽期间本体置半透明。</summary>
        public void SetDragging(bool dragging)
            => _icon.color = dragging ? new Color(1, 1, 1, 0.4f) : Color.white;

        public void OnBeginDrag(PointerEventData eventData)
            => DragSession.Instance?.Begin(_view, this, eventData.position);

        public void OnDrag(PointerEventData eventData)
            => DragSession.Instance?.Update(eventData.position);

        public void OnEndDrag(PointerEventData eventData)
            => DragSession.Instance?.End();

        private static (int x, int y) GetBounds(List<(int row, int col)> cells)
        {
            int minR = int.MaxValue, maxR = int.MinValue;
            int minC = int.MaxValue, maxC = int.MinValue;
            foreach (var (r, c) in cells)
            {
                minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r);
                minC = Mathf.Min(minC, c); maxC = Mathf.Max(maxC, c);
            }
            return (maxC - minC, maxR - minR);
        }
    }
}
