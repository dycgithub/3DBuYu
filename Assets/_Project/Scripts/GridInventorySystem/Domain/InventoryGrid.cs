using System;
using System.Collections.Generic;
using UnityEngine;
using ItemSystem;

namespace InventorySystem
{
    /// <summary>
    /// M×N 网格背包数据结构。
    /// 纯 C# 类，不依赖 MonoBehaviour。管理物品的放置、移除和查询。
    ///
    /// 放置算法：
    /// 1. CanPlaceAt: 遍历 shape 的每个 cell → 检查越界/占用/栏位兼容
    /// 2. PlaceItem: 分配 instanceId → 写入 Slots → 记录到 placedItems
    /// 3. FindFirstFit: 从 (0,0) 开始扫描，返回第一个可放置的位置
    /// </summary>
    public class InventoryGrid
    {
        #region Properties

        public int Width { get; private set; }
        public int Height { get; private set; }
        public GridCell[,] Slots { get; private set; }

        #endregion

        #region Internal State

        private Dictionary<int, PlacedItem> placedItems;      // instanceId → PlacedItem
        private Dictionary<int, ItemConfig> itemConfigs;       // instanceId → config
        private int nextInstanceId = 0;

        #endregion

        #region Constructor

        public InventoryGrid(int width, int height)
        {
            Width = width;
            Height = height;
            Slots = new GridCell[height, width];
            placedItems = new Dictionary<int, PlacedItem>();
            itemConfigs = new Dictionary<int, ItemConfig>();

            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    Slots[r, c] = new GridCell
                    {
                        row = r,
                        col = c,
                        itemInstanceId = -1
                    };
                }
            }
        }

        #endregion

        #region Placement

        /// <summary>
        /// 检查物品形状是否可以放置在指定的 (row, col) 位置。
        /// </summary>
        /// <param name="shape">物品形状。</param>
        /// <param name="row">放置的起始行。</param>
        /// <param name="col">放置的起始列。</param>
        /// <param name="excludeInstanceId">排除的实例 ID（用于拖拽移动时忽略自身占用）。</param>
        /// <returns>如果可放置则返回 true。</returns>
        public bool CanPlaceAt(ItemShape shape, int row, int col, int excludeInstanceId = -1)
        {
            if (shape == null) return false;

            var cells = shape.GetOccupiedCells();
            if (cells.Count == 0) return false;

            return CanPlaceCells(cells, row, col, excludeInstanceId);
        }

        /// <summary>
        /// 放置物品到网格。
        /// </summary>
        /// <param name="config">物品配置。</param>
        /// <param name="row">起始行。</param>
        /// <param name="col">起始列。</param>
        /// <returns>新分配的实例 ID，如果放置失败则返回 -1。</returns>
        public int PlaceItem(ItemConfig config, int row, int col)
        {
            return PlaceItem(config, row, col, 0);
        }

        /// <summary>
        /// 计算物品形状顺时针旋转 N 次（每次 90°）后的占格列表。
        /// 统一旋转算法入口，供放置、拖拽幽灵、裁决共用。
        /// </summary>
        public static List<(int, int)> GetRotatedCells(ItemShape shape, int rotation)
        {
            var cells = shape.GetOccupiedCells();
            int w = shape.Width;
            int h = shape.Height;
            for (int t = 0; t < rotation; t++)
            {
                var rotated = new List<(int, int)>();
                foreach (var (r, c) in cells)
                    rotated.Add((c, w - 1 - r));
                cells = rotated;
                (w, h) = (h, w);
            }
            return cells;
        }

        /// <summary>
        /// 检查物品（含旋转）是否可以放置在指定的 (row, col) 位置。
        /// 边界 + 占用判断，excludeInstanceId 用于同网格拖拽移动时忽略自身。
        /// </summary>
        public bool CanPlaceAt(ItemConfig config, int row, int col, int rotation, int excludeInstanceId = -1)
        {
            if (config == null || config.shape == null) return false;

            var cells = GetRotatedCells(config.shape, rotation);
            if (cells.Count == 0) return false;

            return CanPlaceCells(cells, row, col, excludeInstanceId);
        }

        private bool CanPlaceCells(List<(int, int)> cells, int row, int col, int excludeInstanceId = -1)
        {
            foreach (var (offsetRow, offsetCol) in cells)
            {
                int gridRow = row + offsetRow;
                int gridCol = col + offsetCol;

                // 越界检查
                if (gridRow < 0 || gridRow >= Height || gridCol < 0 || gridCol >= Width)
                    return false;

                var slot = Slots[gridRow, gridCol];

                // 占用检查（允许自身重叠用于拖拽移动）
                if (!slot.IsEmpty && slot.itemInstanceId != excludeInstanceId)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 放置物品到网格（支持旋转）。
        /// </summary>
        public int PlaceItem(ItemConfig config, int row, int col, int rotation)
        {
            if (config == null || config.shape == null) return -1;

            var cells = GetRotatedCells(config.shape, rotation);

            if (!CanPlaceCells(cells, row, col))
                return -1;

            int instanceId = nextInstanceId++;

            foreach (var (offsetRow, offsetCol) in cells)
            {
                int gridRow = row + offsetRow;
                int gridCol = col + offsetCol;
                Slots[gridRow, gridCol].itemInstanceId = instanceId;
            }

            placedItems[instanceId] = new PlacedItem
            {
                instanceId = instanceId,
                itemConfigId = config.itemId,
                row = row,
                col = col,
                rotation = rotation
            };
            itemConfigs[instanceId] = config;

            return instanceId;
        }

        /// <summary>
        /// 从网格中移除物品。
        /// </summary>
        public bool RemoveItem(int instanceId)
        {
            if (!placedItems.TryGetValue(instanceId, out var placedItem))
                return false;

            var config = itemConfigs[instanceId];
            if (config?.shape == null) return false;

            var cells = config.shape.GetOccupiedCells();

            foreach (var (offsetRow, offsetCol) in cells)
            {
                int gridRow = placedItem.row + offsetRow;
                int gridCol = placedItem.col + offsetCol;

                if (gridRow >= 0 && gridRow < Height && gridCol >= 0 && gridCol < Width)
                {
                    if (Slots[gridRow, gridCol].itemInstanceId == instanceId)
                    {
                        Slots[gridRow, gridCol].itemInstanceId = -1;
                    }
                }
            }

            placedItems.Remove(instanceId);
            itemConfigs.Remove(instanceId);
            return true;
        }

        public bool MoveItemRotated(int instanceId, int newRow, int newCol, System.Collections.Generic.List<(int row, int col)> rotatedCells, int newRotation)
        {
            if (!placedItems.TryGetValue(instanceId, out var placedItem)) return false;
            if (!itemConfigs.TryGetValue(instanceId, out var config)) return false;

            if (config.shape == null) return false;

            var originalCells = config.shape.GetOccupiedCells();

            foreach (var (offsetRow, offsetCol) in originalCells)
            {
                int oldRow = placedItem.row + offsetRow;
                int oldCol = placedItem.col + offsetCol;
                if (oldRow >= 0 && oldRow < Height && oldCol >= 0 && oldCol < Width)
                {
                    if (Slots[oldRow, oldCol].itemInstanceId == instanceId)
                        Slots[oldRow, oldCol].itemInstanceId = -1;
                }
            }

            foreach (var (offsetRow, offsetCol) in rotatedCells)
            {
                int gridRow = newRow + offsetRow;
                int gridCol = newCol + offsetCol;
                if (gridRow < 0 || gridRow >= Height || gridCol < 0 || gridCol >= Width)
                {
                    foreach (var (or, oc) in originalCells)
                    {
                        int rr = placedItem.row + or;
                        int rc = placedItem.col + oc;
                        if (rr >= 0 && rr < Height && rc >= 0 && rc < Width)
                            Slots[rr, rc].itemInstanceId = instanceId;
                    }
                    return false;
                }

                if (!Slots[gridRow, gridCol].IsEmpty && Slots[gridRow, gridCol].itemInstanceId != instanceId)
                {
                    foreach (var (or, oc) in originalCells)
                    {
                        int rr = placedItem.row + or;
                        int rc = placedItem.col + oc;
                        if (rr >= 0 && rr < Height && rc >= 0 && rc < Width)
                            Slots[rr, rc].itemInstanceId = instanceId;
                    }
                    return false;
                }

                Slots[gridRow, gridCol].itemInstanceId = instanceId;
            }

            placedItems[instanceId] = new PlacedItem
            {
                instanceId = instanceId,
                itemConfigId = placedItem.itemConfigId,
                row = newRow,
                col = newCol,
                rotation = newRotation
            };

            return true;
        }

        #endregion

        #region Queries

        /// <summary>
        /// 查找第一个可以放置给定形状的空位。
        /// 类型规则（如 Skill/Ammunition）由 BaseInventory 的 Validator 策略在调用前校验。
        /// </summary>
        /// <param name="shape">物品形状。</param>
        /// <returns>(row, col)，如果找不到则返回 (-1, -1)。</returns>
        public (int row, int col) FindFirstFit(ItemShape shape)
        {
            if (shape == null) return (-1, -1);

            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    if (CanPlaceAt(shape, r, c))
                    {
                        return (r, c);
                    }
                }
            }
            return (-1, -1);
        }

        /// <summary>
        /// 获取所有已放置的物品。
        /// </summary>
        public IReadOnlyList<PlacedItem> GetAllItems()
        {
            var items = new List<PlacedItem>(placedItems.Values);
            return items;
        }

        /// <summary>
        /// 获取指定实例 ID 的物品配置。
        /// </summary>
        public ItemConfig GetItemConfig(int instanceId)
        {
            itemConfigs.TryGetValue(instanceId, out var config);
            return config;
        }

        /// <summary>
        /// 获取指定实例 ID 的放置信息。
        /// </summary>
        public PlacedItem? GetPlacedItem(int instanceId)
        {
            if (placedItems.TryGetValue(instanceId, out var item))
                return item;
            return null;
        }

        /// <summary>
        /// 获取指定格子处的物品配置（如果该格子被占用）。
        /// 用于 UI 查询：拖拽放置前检查目标格子是否已被占用。
        /// </summary>
        /// <param name="row">行索引</param>
        /// <param name="col">列索引</param>
        /// <returns>物品配置，若格子为空则返回 null。</returns>
        public ItemConfig GetItemConfigAt(int row, int col)
        {
            if (row < 0 || row >= Height || col < 0 || col >= Width)
                return null;

            var slot = Slots[row, col];
            if (slot.IsEmpty)
                return null;

            itemConfigs.TryGetValue(slot.itemInstanceId, out var config);
            return config;
        }

        /// <summary>
        /// 检查是否有足够空间放置给定物品。
        /// </summary>
        public bool HasFreeSlot(ItemShape shape)
        {
            var (r, c) = FindFirstFit(shape);
            return r >= 0;
        }

        #endregion

        #region Serialization

        /// <summary>
        /// 序列化为可保存的数组。
        /// </summary>
        public PlacedItem[] ToSaveData()
        {
            var data = new PlacedItem[placedItems.Count];
            placedItems.Values.CopyTo(data, 0);
            return data;
        }

        /// <summary>
        /// 从存档数据加载。
        /// </summary>
        public void LoadFromData(PlacedItem[] data, ItemConfigRegistry registry)
        {
            Clear();

            foreach (var item in data)
            {
                if (registry.TryGet(item.itemConfigId, out var config))
                {
                    PlaceItem(config, item.row, item.col, item.rotation);
                }
            }
        }

        /// <summary>
        /// 清空整个网格。
        /// </summary>
        public void Clear()
        {
            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    Slots[r, c].itemInstanceId = -1;
                }
            }
            placedItems.Clear();
            itemConfigs.Clear();
        }

        #endregion
    }
}
