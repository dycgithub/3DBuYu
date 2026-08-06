using System;

namespace InventorySystem.GridSnap
{
    /// <summary>
    /// 吸附网格格子坐标(纯 C#)。
    /// </summary>
    public readonly struct SnapCell : IEquatable<SnapCell>
    {
        public readonly int Row;
        public readonly int Col;

        public SnapCell(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public static SnapCell operator -(SnapCell a, SnapCell b)
            => new SnapCell(a.Row - b.Row, a.Col - b.Col);

        public bool Equals(SnapCell other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object obj) => obj is SnapCell c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(Row, Col);
        public override string ToString() => $"({Row},{Col})";
    }
}
