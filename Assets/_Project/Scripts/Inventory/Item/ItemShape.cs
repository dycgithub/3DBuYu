using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物品形状类型(对齐 Cholopol TIS 的 TetrisPieceShape):
/// 形状点集由 ItemShapeSet 集中定义,多个物品可复用同一形状。
/// </summary>
public enum ItemShape
{
    Single,        // 1×1
    Vertical2,     // 竖 2
    Horizontal2,   // 横 2
    Square2x2,     // 2×2 方块
    LShape1,        // L 形
    LShape2,        // L 形
    LShape3,        // L 形
    TShape1,        // T 形
    TShape2,        // T 形

}

/// <summary>形状枚举 → 点集的映射项(序列化数据,供 ItemShapeSet 资产配置)。</summary>
[Serializable]
public class PointSet
{
    /// <summary>形状枚举。</summary>
    public ItemShape Shape;

    /// <summary>点集约定:x=列偏移, y=行偏移(向右/向下为正),以包围盒左上角为原点。</summary>
    public List<Vector2Int> Points = new();
}
