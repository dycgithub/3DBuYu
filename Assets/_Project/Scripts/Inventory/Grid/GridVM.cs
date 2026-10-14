using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 网格数据(VM,纯逻辑):维护 列×行 的占用二维数组和成功放置顺序。
/// 边界检查 / 占用检查 / 放置 / 移除都在这里,不依赖任何 UI。
/// </summary>
public class GridVM
{
    public int Width { get; } // 列数
    public int Height { get; } // 行数
    public GridType GridType { get; }

    private readonly ItemVM[,] _cells; // [列, 行]
    private readonly HashSet<ItemVM> _occupants = new(); // 占用物品去重集合
    private readonly List<ItemVM> _orderedOccupants = new(); // 成功放置顺序
    private readonly Dictionary<ItemVM, long> _placementOrder = new(); // 拖拽回滚后仍保持的放置序号
    private long _nextPlacementOrder;

    /// <summary>网格内物品数量(去重:一个物品占多格只计一次)。</summary>
    public int ItemCount => _orderedOccupants.Count;

    /// <summary>网格内全部物品(去重,按成功放置顺序排列)。</summary>
    public IEnumerable<ItemVM> Items => _orderedOccupants;

    /// <summary>按 (列, 行) 查询格子,返回占用它的物品(null=空)。</summary>
    public ItemVM this[int col, int row] => _cells[col, row];
    public GridVM(int width, int height, GridType gridType)
    {
        Width = width;
        Height = height;
        GridType = gridType;
        _cells = new ItemVM[width, height];
    }

    /// <summary>以 (posX, posY) 为左上角、shapeWidth×shapeHeight 的矩形是否完全在网格内。</summary>
    public bool BoundaryCheck(int posX, int posY, int shapeWidth, int shapeHeight)
    {
        return posX >= 0 && posY >= 0 && posX + shapeWidth <= Width && posY + shapeHeight <= Height;
    }

    /// <summary>物品能否放在锚点 (posX, posY):边界检查 + 逐格占用检查。</summary>
    public bool CanPlace(ItemVM item, int posX, int posY)
    {
        if (item == null)
        {
            return false;
        }

        if (!ItemPlacementPermissions.Allows(GridType, item.Definition))
        {
            return false;
        }

        if (!BoundaryCheck(posX, posY, item.Width, item.Height))
        {
            return false;
        }

        foreach (var p in item.CoordinateSet)
        {
            int x = posX + p.x + item.RotationOffset.x;
            int y = posY + p.y + item.RotationOffset.y;
            ItemVM occupant = _cells[x, y];
            if (occupant != null && occupant != item)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>放置物品(写占用 + 记录坐标)。失败(越界/占用)返回 false。</summary>
    public bool Place(ItemVM item, int posX, int posY)
    {
        if (!CanPlace(item, posX, posY))
        {
            return false;
        }

        ClearCells(item);
        item.LocalGridCoordinate = new Vector2Int(posX, posY);
        foreach (var p in item.CoordinateSet)
        {
            int x = posX + p.x + item.RotationOffset.x;
            int y = posY + p.y + item.RotationOffset.y;
            _cells[x, y] = item;
        }

        if (_occupants.Add(item))
        {
            if (!_placementOrder.TryGetValue(item, out long order))
            {
                order = ++_nextPlacementOrder;
                _placementOrder.Add(item, order);
            }

            InsertInPlacementOrder(item, order);
        }
        return true;
    }

    /// <summary>从网格移除物品(清空它占用的所有格子)。</summary>
    public void Remove(ItemVM item)
    {
        if (item == null)
        {
            return;
        }

        ClearCells(item);
        _occupants.Remove(item);
        _orderedOccupants.Remove(item);
        _placementOrder.Remove(item);
    }

    /// <summary>暂时清空占用格；发射器背包保留顺序，供拖拽失败时原样回滚。</summary>
    /// <param name="item">当前正在拖拽的物品。</param>
    /// <returns>物品属于当前网格时返回 true。</returns>
    internal bool SuspendPlacement(ItemVM item)
    {
        if (item == null || !_occupants.Contains(item))
        {
            return false;
        }

        if (!_placementOrder.ContainsKey(item))
        {
            return false;
        }

        int previousOrder = _orderedOccupants.IndexOf(item);
        if (previousOrder < 0)
        {
            return false;
        }

        _occupants.Remove(item);
        _orderedOccupants.RemoveAt(previousOrder);
        return true;
    }

    /// <summary>检查物品是否处于可回滚的拖拽暂停状态。</summary>
    /// <param name="item">待检查的物品。</param>
    /// <returns>网格仍保存该物品的原顺序时返回 true。</returns>
    internal bool IsSuspended(ItemVM item)
    {
        return item != null && _placementOrder.ContainsKey(item) && !_occupants.Contains(item);
    }

    /// <summary>清空网格数据及尚未结束的拖拽事务。</summary>
    internal void Clear()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                _cells[x, y] = null;
            }
        }

        _occupants.Clear();
        _orderedOccupants.Clear();
        _placementOrder.Clear();
    }

    /// <summary>清空物品在占用数组中的所有格子。</summary>
    /// <param name="item">要清空的网格物品。</param>
    private void ClearCells(ItemVM item)
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (_cells[x, y] == item)
                {
                    _cells[x, y] = null;
                }
            }
        }
    }

    /// <summary>按物品稳定的放置序号插入当前网格集合。</summary>
    /// <param name="item">要登记的物品。</param>
    /// <param name="order">该物品的放置序号。</param>
    private void InsertInPlacementOrder(ItemVM item, long order)
    {
        int insertIndex = _orderedOccupants.Count;
        for (int index = 0; index < _orderedOccupants.Count; index++)
        {
            if (_placementOrder[_orderedOccupants[index]] > order)
            {
                insertIndex = index;
                break;
            }
        }

        _orderedOccupants.Insert(insertIndex, item);
    }

    /// <summary>查询某个格子被哪个物品占用(等价于索引器,语义更直白)。</summary>
    public ItemVM GetOccupant(int col, int row) => _cells[col, row];

    public bool Contains(ItemVM item) => item != null && _occupants.Contains(item);
}
