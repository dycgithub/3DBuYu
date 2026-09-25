using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SpatialIndexing
{
    /// <summary>
    /// 连续单元区间网格。所有读写者共享 Fence，写入、扩容和释放前必须 Complete。
    /// 单元只缩小候选集，边界外位置夹到边界单元，精确距离仍使用原坐标。
    /// </summary>
    internal sealed class ContinuousSpatialGrid : IDisposable
    {
        internal NativeArray<byte> Active;
        internal NativeArray<float3> Positions;
        internal NativeArray<float3> Velocities;
        internal NativeArray<int> Starts;
        internal NativeArray<int> Slots;
        private NativeArray<int> _counts;
        private NativeArray<int> _heads;
        internal GridLayout Layout;
        internal JobHandle Fence;

        /// <summary>完成已有使用者后配置布局和容量；不限制每个单元的条目数。</summary>
        internal void Configure(float3 origin, float cellSize, int3 dimensions, int capacity)
        {
            Complete();
            Layout = new GridLayout(origin, cellSize, dimensions);
            if (!Active.IsCreated || Active.Length < capacity)
            {
                int size = math.max(capacity, Active.IsCreated ? Active.Length * 2 : 16);
                Grow(ref Active, size);
                Grow(ref Positions, size);
                Grow(ref Velocities, size);
                Grow(ref Slots, size);
            }

            if (!_counts.IsCreated || _counts.Length != Layout.CellCount)
            {
                Resize(ref _counts, Layout.CellCount);
                Resize(ref _heads, Layout.CellCount);
                Resize(ref Starts, Layout.CellCount + 1);
            }
        }

        /// <summary>按计数、前缀和、散射构建连续区间，返回所有后续读者必须依赖的句柄。</summary>
        internal JobHandle Build(JobHandle dependency = default)
        {
            Fence = new BuildSpatialGridJob
            {
                Active = Active, Positions = Positions, Counts = _counts,
                Heads = _heads, Starts = Starts, Slots = Slots, Layout = Layout
            }.Schedule(JobHandle.CombineDependencies(Fence, dependency));
            return Fence;
        }

        /// <summary>完成快照后精确查询，结果列表自动增长，无固定缓冲区截断。</summary>
        internal void Query(float3 center, float radius, List<int> results)
        {
            Complete();
            results.Clear();
            if (!Starts.IsCreated || radius < 0f)
            {
                return;
            }

            int3 min = Layout.Cell(center - radius);
            int3 max = Layout.Cell(center + radius);
            float squared = radius * radius;
            for (int z = min.z; z <= max.z; z++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        int cell = Layout.Index(new int3(x, y, z));
                        for (int i = Starts[cell]; i < Starts[cell + 1]; i++)
                        {
                            int slot = Slots[i];
                            if (Active[slot] != 0 && math.distancesq(center, Positions[slot]) <= squared)
                            {
                                results.Add(slot);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>阻止主线程读取或改写仍被 Job 使用的数组。</summary>
        internal void Complete() => Fence.Complete();

        /// <summary>等待所有读写者并释放持久数组。</summary>
        public void Dispose()
        {
            Complete();
            Release(ref Active); Release(ref Positions); Release(ref Velocities);
            Release(ref Starts); Release(ref Slots); Release(ref _counts); Release(ref _heads);
        }

        private static void Grow<T>(ref NativeArray<T> array, int length) where T : struct
        {
            var replacement = new NativeArray<T>(length, Allocator.Persistent);
            if (array.IsCreated)
            {
                NativeArray<T>.Copy(array, replacement, array.Length);
                array.Dispose();
            }
            array = replacement;
        }

        private static void Resize<T>(ref NativeArray<T> array, int length) where T : struct
        {
            Release(ref array);
            array = new NativeArray<T>(length, Allocator.Persistent);
        }

        private static void Release<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
                array = default;
            }
        }
    }
}
