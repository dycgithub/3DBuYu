using CombatSystem;
using UnityEngine;

/// <summary>
/// Down=0° / Left=90° / Up=180° / Right=270°(顺时针)。
/// </summary>
public enum Dir
{
    Down,
    Left,
    Up,
    Right,
}

/// <summary>网格分类。</summary>
public enum GridType
{
    Shop,
    StorageForShop,
    StorageForPlay,
    TransmitterBackpack,
    CentralBackpack,
}

/// <summary>放置失败原因。</summary>
public enum PlacementBlockReason
{
    None,
    OutOfBounds,
    Occupied,
    PointsNotEnough,
    WrongGridType
}

/// <summary>
/// 物品进入战斗网格前的摆放权限。
/// 001 在发射器侧属于 F/N：允许摆放，但不代表拥有发射器效果。
/// </summary>
public static class ItemPlacementPermissions
{
    /// <summary>
    /// 判断物品是否可以进入指定网格类型。
    /// </summary>
    /// <param name="gridType">目标网格类型。</param>
    /// <param name="definition">物品静态定义。</param>
    /// <returns>允许摆放时返回 <c>true</c>。</returns>
    public static bool Allows(GridType gridType, ItemDefinition definition)
    {
        if (gridType != GridType.CentralBackpack && gridType != GridType.TransmitterBackpack)
        {
            return true;
        }

        if (!ItemCombatScopePolicy.TryResolve(definition?.Id, out CombatScope scope))
        {
            return false;
        }

        if (gridType == GridType.CentralBackpack)
        {
            return scope == CombatScope.Central || scope == CombatScope.CentralAndTransmitter;
        }

        return scope == CombatScope.Transmitter || scope == CombatScope.CentralAndTransmitter;
    }
}
