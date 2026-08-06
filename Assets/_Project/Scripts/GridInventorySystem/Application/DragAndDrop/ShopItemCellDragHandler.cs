using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using InventorySystem;
using InventorySystem.Shop;
using ItemSystem;

namespace _Project.UI.Shop
{
    /// <summary>
    /// 商店货架格子的拖拽处理器。
    /// 挂在 ShopItemCell 预制体上，实现从商店货架拖拽商品到背包/装备网格（拖拽即转移）。
    ///
    /// 交互流程：
    ///   拖拽商品 → 按物品形状创建半透明幽灵跟随鼠标（R 键旋转）→ 放到目标网格 → ShopManager.TryPurchaseToSlot()
    /// 幽灵规格与背包拖拽（InventoryDragGhost）一致：每格 60px，第一格显示图标，其余格半透明占位。
    /// </summary>
    public class ShopItemCellDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>此格子对应的货架实例 ID（OnBeginDrag 时从网格解析，空格为 -1）。</summary>
        public int ShopInstanceId { get; private set; } = -1;

        /// <summary>拖拽的物品配置（OnBeginDrag 时从网格解析）。</summary>
        public ItemConfig Item { get; private set; }

        /// <summary>拖拽是否成功（由 Drop 目标设置）。</summary>
        public bool DropSuccess { get; set; }

        /// <summary>当前拖拽旋转状态（0-3，顺时针 90° 步进）。放置目标据此落格。</summary>
        public int Rotation { get; private set; }

        /// <summary>来源货架(用于落点判定:放回同货架 = 免费移动)。</summary>
        public ShopInventory SourceStock => _stock;

        /// <summary>起始行(落点裁决用)。</summary>
        public int Row => _row;

        /// <summary>起始列(落点裁决用)。</summary>
        public int Col => _col;

        [SerializeField] private float _cellSize = 60f;

        private ShopInventory _stock;
        private int _row;
        private int _col;

        private GameObject _ghostRoot;
        private Canvas _rootCanvas;
        private Camera _pressCamera;
        private readonly List<RectTransform> _cellRects = new();
        private bool _dragging;

        public void Initialize(int row, int col, ShopInventory stock)
        {
            _row = row;
            _col = col;
            _stock = stock;
        }

        private void Awake()
        {
            _rootCanvas = GetComponentInParent<Canvas>();
            if (_rootCanvas != null)
                _rootCanvas = _rootCanvas.rootCanvas;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_stock?.Grid == null)
            {
                eventData.pointerDrag = null;
                return;
            }

            var slot = _stock.Grid.Slots[_row, _col];
            if (slot.IsEmpty)
            {
                eventData.pointerDrag = null;
                return;
            }

            ShopInstanceId = slot.itemInstanceId;
            Item = _stock.Grid.GetItemConfig(ShopInstanceId);
            if (Item == null || Item.shape == null)
            {
                eventData.pointerDrag = null;
                return;
            }

            DropSuccess = false;
            Rotation = 0;
            _pressCamera = eventData.pressEventCamera;

            CreateGhost();
            if (_ghostRoot == null) return;

            // 将幽灵放到根 Canvas 下确保渲染在最顶层
            var targetCanvas = _rootCanvas ?? GetComponentInParent<Canvas>();
            if (targetCanvas != null)
                _ghostRoot.transform.SetParent(targetCanvas.transform, false);
            _ghostRoot.transform.SetAsLastSibling();

            _dragging = true;
            FollowCursor(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _ghostRoot == null) return;
            FollowCursor(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            DestroyGhost();
        }

        private void Update()
        {
            if (!_dragging || _ghostRoot == null) return;

            if (Input.GetKeyDown(KeyCode.R))
                Rotate();
        }

        #region Ghost

        /// <summary>
        /// 按物品形状创建占格幽灵：第一格显示物品图标，其余格半透明灰底占位。
        /// </summary>
        private void CreateGhost()
        {
            DestroyGhost();

            var shape = Item?.shape;
            if (shape == null) return;

            _ghostRoot = new GameObject("ShopDragGhost");
            _ghostRoot.transform.SetParent(transform.parent, false);

            // 幽灵不拦截事件，保证事件穿透到目标格子的 OnDrop。
            var canvasGroup = _ghostRoot.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var rootRect = _ghostRoot.AddComponent<RectTransform>();
            rootRect.pivot = Vector2.zero;

            var cells = InventoryGrid.GetRotatedCells(shape, Rotation);
            for (int i = 0; i < cells.Count; i++)
            {
                var (r, c) = cells[i];

                var cellGo = new GameObject($"Cell_{r}_{c}");
                var cellRect = cellGo.AddComponent<RectTransform>();
                cellRect.SetParent(rootRect, false);
                cellRect.sizeDelta = new Vector2(_cellSize, _cellSize);
                cellRect.anchoredPosition = new Vector2(c * _cellSize, -r * _cellSize);

                var image = cellGo.AddComponent<Image>();
                image.raycastTarget = false;

                // 每个占格都显示相同的道具 icon(形状由 icon 拼图表示)。
                image.sprite = _Project.UI.Inventory.ItemVisualHelper.GetIcon(Item.itemId);
                image.color = Color.white;

                _cellRects.Add(cellRect);
            }
        }

        /// <summary>R 键旋转：更新旋转状态并重新布局幽灵占格（格数不变，仅位置变化）。</summary>
        private void Rotate()
        {
            if (Item?.shape == null || _ghostRoot == null) return;

            Rotation = (Rotation + 1) % 4;

            var cells = InventoryGrid.GetRotatedCells(Item.shape, Rotation);
            int minR = int.MaxValue, minC = int.MaxValue;
            foreach (var (r, c) in cells)
            {
                if (r < minR) minR = r;
                if (c < minC) minC = c;
            }

            for (int i = 0; i < _cellRects.Count && i < cells.Count; i++)
            {
                var (nr, nc) = (cells[i].Item1 - minR, cells[i].Item2 - minC);
                _cellRects[i].anchoredPosition = new Vector2(nc * _cellSize, -nr * _cellSize);
            }
        }

        private void FollowCursor(Vector2 screenPosition)
        {
            if (_ghostRoot == null) return;

            var parent = _ghostRoot.transform.parent as RectTransform;
            if (parent == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenPosition, _pressCamera, out Vector2 localPoint))
            {
                _ghostRoot.GetComponent<RectTransform>().anchoredPosition = localPoint;
            }
        }

        private void DestroyGhost()
        {
            if (_ghostRoot != null)
            {
                Destroy(_ghostRoot);
                _ghostRoot = null;
            }
            _cellRects.Clear();
        }

        #endregion
    }
}
