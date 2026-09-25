using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SpatialIndexing
{
    /// <summary>复用群游网格的计数排序：每个活动条目恰好占据一个连续槽位。</summary>
    [BurstCompile]
    internal struct BuildSpatialGridJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Active;
        [ReadOnly] public NativeArray<float3> Positions;
        public NativeArray<int> Counts;
        public NativeArray<int> Heads;
        public NativeArray<int> Starts;
        public NativeArray<int> Slots;
        public GridLayout Layout;

        public void Execute()
        {
            for (int cell = 0; cell < Layout.CellCount; cell++)
            {
                Counts[cell] = 0;
            }
            for (int slot = 0; slot < Active.Length; slot++)
            {
                if (Active[slot] != 0)
                {
                    Counts[Layout.Index(Layout.Cell(Positions[slot]))]++;
                }
            }
            int cursor = 0;
            for (int cell = 0; cell < Layout.CellCount; cell++)
            {
                Starts[cell] = cursor;
                Heads[cell] = cursor;
                cursor += Counts[cell];
            }
            Starts[Layout.CellCount] = cursor;
            for (int slot = 0; slot < Active.Length; slot++)
            {
                if (Active[slot] != 0)
                {
                    int cell = Layout.Index(Layout.Cell(Positions[slot]));
                    Slots[Heads[cell]++] = slot;
                }
            }
        }
    }
}
