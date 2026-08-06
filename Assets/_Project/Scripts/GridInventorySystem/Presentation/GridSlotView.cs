using _Project.UI.Shop;
using InventorySystem.GridSnap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace _Project.UI.Inventory
{
    /// <summary>
    /// 格子视图:统一网格的单个格子,持有 Drop 目标。
    /// 坐标由 GridLayoutMath 按步长计算,保证所有网格排列一致。
    /// </summary>
    public class GridSlotView : MonoBehaviour, IDropHandler
    {
        [SerializeField] private Image _background;

        private InventoryGridView _view;
        private int _row;
        private int _col;

        public RectTransform Rect { get; private set; }

        /// <summary>格子行(落点裁决用)。</summary>
        public int Row => _row;

        /// <summary>格子列(落点裁决用)。</summary>
        public int Col => _col;

        private void Awake() => Rect = GetComponent<RectTransform>();

        /// <summary>绑定网格并定位到 (row,col)。</summary>
        public void Initialize(InventoryGridView view, int row, int col)
        {
            _view = view;
            _row = row;
            _col = col;
            Rect.anchoredPosition = GridLayoutMath.CellLocalPos(row, col, view.Step);
            _background.color = view.EmptyColor;
        }

        /// <summary>格子是否被物品占用(用于底色反馈)。</summary>
        public void SetOccupied(bool occupied)
            => _background.color = occupied ? _view.OccupiedColor : _view.EmptyColor;

        /// <summary>拖拽释放:背包类交给 DragSession,商店类交给 ShopManager(购买/货架移动)。</summary>
        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;

            if (eventData.pointerDrag.TryGetComponent<ShopItemCellDragHandler>(out var shopDrag))
            {
                DragSession.Instance?.DropShopOn(_view, shopDrag);
                return;
            }

            DragSession.Instance?.DropOn(_view);
        }
    }
}
