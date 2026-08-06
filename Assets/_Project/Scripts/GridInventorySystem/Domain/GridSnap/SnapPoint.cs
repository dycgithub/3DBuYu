namespace InventorySystem.GridSnap
{
    /// <summary>
    /// 网格容器内的本地坐标。约定:X 向右为正,Y 向下为正(视觉坐标)。
    /// 与 UnityEngine.Vector2 的差异在于 Y 轴方向,便于 row/col 直觉换算。
    /// </summary>
    public readonly struct SnapPoint
    {
        public readonly float X;
        public readonly float Y;

        public SnapPoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
