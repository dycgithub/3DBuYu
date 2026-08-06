namespace InventorySystem.GridSnap
{
    /// <summary>
    /// 一次吸附计算的不可变结果,经 R3 流广播。
    /// </summary>
    public readonly struct GridSnapState
    {
        /// <summary>鼠标所在格。</summary>
        public readonly SnapCell HoverCell;

        /// <summary>物品 (0,0) 锚定格。</summary>
        public readonly SnapCell AnchorCell;

        /// <summary>形状+边界+占用判定结果。</summary>
        public readonly bool IsValid;

        /// <summary>锚定格左上角的容器本地坐标(供幽灵定位)。</summary>
        public readonly SnapPoint LocalOrigin;

        public GridSnapState(SnapCell hover, SnapCell anchor, bool valid, SnapPoint origin)
        {
            HoverCell = hover;
            AnchorCell = anchor;
            IsValid = valid;
            LocalOrigin = origin;
        }
    }
}
