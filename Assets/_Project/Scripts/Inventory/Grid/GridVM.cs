using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 网格数据(VM,纯逻辑):维护 列×行 的占用二维数组。
/// 边界检查 / 占用检查 / 放置 / 移除都在这里,不依赖任何 UI。
/// </summary>
public class GridVM
{
    public int Width { get; } // 列数
    public int Height { get; } // 行数
    public GridType GridType { get; }

    private readonly ItemVM[,] _cells; // [列, 行]
    private readonly HashSet<ItemVM> _occupants = new(); // 占用物品去重集合

    /// <summary>网格内物品数量(去重:一个物品占多格只计一次)。</summary>
    public int ItemCount => _occupants.Count;

    /// <summary>网格内全部物品(去重,遍历顺序不定)。</summary>
    public IEnumerable<ItemVM> Items => _occupants;

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
        if (item == null) return false;
        if (!BoundaryCheck(posX, posY, item.Width, item.Height)) return false;
        foreach (var p in item.CoordinateSet)
        {
            int x = posX + p.x + item.RotationOffset.x;
            int y = posY + p.y + item.RotationOffset.y;
            if (_cells[x, y] != null) return false;
        }

        return true;
    }

    /// <summary>放置物品(写占用 + 记录坐标)。失败(越界/占用)返回 false。</summary>
    public bool Place(ItemVM item, int posX, int posY)
    {
        if (!CanPlace(item, posX, posY)) return false;
        item.LocalGridCoordinate = new Vector2Int(posX, posY);
        foreach (var p in item.CoordinateSet)
        {
            int x = posX + p.x + item.RotationOffset.x;
            int y = posY + p.y + item.RotationOffset.y;
            _cells[x, y] = item;
        }

        _occupants.Add(item);
        return true;
    }

    /// <summary>从网格移除物品(清空它占用的所有格子)。</summary>
    public void Remove(ItemVM item)
    {
        if (item == null) return;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (_cells[x, y] == item) _cells[x, y] = null;
            }
        }

        _occupants.Remove(item);
    }

    /// <summary>查询某个格子被哪个物品占用(等价于索引器,语义更直白)。</summary>
    public ItemVM GetOccupant(int col, int row) => _cells[col, row];

    public bool Contains(ItemVM item) => item != null && _occupants.Contains(item);
}
