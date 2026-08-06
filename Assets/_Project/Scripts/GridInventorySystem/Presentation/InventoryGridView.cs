using System.Collections.Generic;
using InventorySystem;
using Interfaces;
using UnityEngine;

namespace _Project.UI.Inventory
{
    /// <summary>
    /// 统一网格视图:所有库存(仓库/炮塔/端口/商店)由同一脚本按同一参数生成,
    /// 保证格子大小与排列一致。响应 OnInventoryChanged 增量刷新物品视图。
    /// 物品用独立 prefab(ItemSlotView),不再逐格贴 icon。
    /// </summary>
    public class InventoryGridView : MonoBehaviour
    {
        [Header("Prefab 引用")]
        [SerializeField] private GridSlotView _slotPrefab;
        [SerializeField] private ItemSlotView _itemSlotPrefab;
        [SerializeField] private ItemGhostView _ghostPrefab;

        [Header("网格布局(全项目统一数值)")]
        [SerializeField] private float _cellSize = 60f;
        [SerializeField] private float _spacing = 4f;

        [Header("配色")]
        [SerializeField] private Color _emptyColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        [SerializeField] private Color _occupiedColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);

        public float CellSize => _cellSize;
        public float Spacing => _spacing;

        /// <summary>格子步长 = cellSize + spacing,所有格子/物品/幽灵共用。</summary>
        public float Step => _cellSize + _spacing;

        public Color EmptyColor => _emptyColor;
        public Color OccupiedColor => _occupiedColor;
        public ItemGhostView GhostPrefab => _ghostPrefab;
        public IInventory Inventory => _inventory;
        public RectTransform Container => (RectTransform)transform;

        /// <summary>根 Canvas(幽灵挂载于此,保证渲染最顶层)。</summary>
        public Canvas RootCanvas { get; private set; }

        private IInventory _inventory;
        private GridSlotView[,] _slots;
        private readonly List<ItemSlotView> _itemViews = new();

        private void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            RootCanvas = canvas != null ? canvas.rootCanvas : null;
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= RefreshAll;
        }

        /// <summary>绑定库存并重建网格。任何 IInventory(仓库/炮塔/端口/商店)都可直接喂入。</summary>
        public void Initialize(IInventory inventory)
        {
            if (inventory == null) return;
            if (_inventory != null)
                _inventory.OnInventoryChanged -= RefreshAll;

            _inventory = inventory;
            _inventory.OnInventoryChanged += RefreshAll;

            BuildGrid();
            RefreshAll();
        }

        private void BuildGrid()
        {
            ClearChildren();
            var grid = _inventory.Grid;
            _slots = new GridSlotView[grid.Height, grid.Width];

            for (int r = 0; r < grid.Height; r++)
            {
                for (int c = 0; c < grid.Width; c++)
                {
                    var slot = Instantiate(_slotPrefab, transform);
                    slot.name = $"Slot_{r}_{c}";
                    slot.Initialize(this, r, c);
                    _slots[r, c] = slot;
                }
            }
        }

        private void RefreshAll()
        {
            if (_inventory?.Grid == null) return;
            var grid = _inventory.Grid;

            // 1) 格子底色
            for (int r = 0; r < grid.Height; r++)
            {
                for (int c = 0; c < grid.Width; c++)
                {
                    _slots[r, c].SetOccupied(!grid.Slots[r, c].IsEmpty);
                }
            }

            // 2) 物品视图:全量重建(网格规模小;后续可换 ObjectPool)
            foreach (var item in _itemViews)
                if (item != null) Destroy(item.gameObject);
            _itemViews.Clear();

            foreach (var placed in grid.GetAllItems())
            {
                var item = Instantiate(_itemSlotPrefab, transform);
                item.name = $"Item_{placed.instanceId}";
                item.Bind(this, placed);
                _itemViews.Add(item);
            }
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
        }
    }
}
