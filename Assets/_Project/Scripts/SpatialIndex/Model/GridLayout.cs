using Unity.Mathematics;

namespace SpatialIndexing
{
    /// <summary>独立于 ECS 和敌人配置的规则三维网格坐标映射。</summary>
    internal readonly struct GridLayout
    {
        internal readonly float3 Origin;
        internal readonly float CellSize;
        internal readonly int3 Dimensions;
        internal int CellCount => Dimensions.x * Dimensions.y * Dimensions.z;

        internal GridLayout(float3 origin, float cellSize, int3 dimensions)
        {
            Origin = origin;
            CellSize = math.max(cellSize, 0.0001f);
            Dimensions = math.max(dimensions, new int3(1));
        }

        /// <summary>返回夹到布局边界的单元坐标。</summary>
        internal int3 Cell(float3 position) => math.clamp(
            (int3)math.floor((position - Origin) / CellSize), int3.zero, Dimensions - 1);

        /// <summary>将单元坐标展开为连续数组索引。</summary>
        internal int Index(int3 cell) => cell.x + Dimensions.x * (cell.y + Dimensions.y * cell.z);
    }
}
