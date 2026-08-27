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
    PointsNotEnough
}