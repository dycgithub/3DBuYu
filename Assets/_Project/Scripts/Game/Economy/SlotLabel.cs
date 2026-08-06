namespace GameSystem
{
    /// <summary>
    /// 库存存档分区标识:区分同一 inventory.json 中不同库存的数据段。
    /// Storage = 玩家仓库;TurretBag = 炮塔装备格;PortBag_0..N = 各炮口装备格。
    /// </summary>
    public enum SlotLabel
    {
        Storage,
        TurretBag,
        PortBag,
    }
}
