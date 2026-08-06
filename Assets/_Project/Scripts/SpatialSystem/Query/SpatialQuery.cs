using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using SpatialSystem.Data;

namespace SpatialSystem.Query
{
    /// <summary>
    /// 无状态的空间查询方法（Bur`st 编译）。
    /// 核心查询逻辑使用 Burst 编译以获得极致性能。
    /// 托管兼容包装器处理 Vector3 ↔ float3 和数组 ↔ NativeArray 转换。
    ///
    /// 每层都有迭代上限保护，防止数据损坏或大范围查询导致死循环。
    /// </summary>
    [BurstCompile]
    public static class SpatialQuery
    {
        /// <summary>
        /// 硬安全上限：任何单次查询的最大总迭代次数。
        /// 超过此上限时查询会优雅终止，而不是死循环。
        /// </summary>
        public const int MAX_ITERATIONS = 4096;

        /// <summary>
        /// 最大可返回的查询结果数量。
        /// </summary>
        public const int MAX_RESULTS = 256;

        #region Burst-Compiled Core

        /// <summary>
        /// [BurstCompile] 半径查询核心：收集中心点周围给定半径内的所有活跃实体。
        /// 所有参数必须为 Burst 兼容类型（NativeArray + float3）。
        /// </summary>
        [BurstCompile]
        public static int QueryRadiusBurst(
            in NativeArray<SpatialCell> buckets,
            in NativeArray<SpatialEntry> entries,
            int activeEntryCount,
            int cellsX, int cellsY, int cellsZ,
            float cellSize, in float3 origin,
            in float3 center, float radius, int layerMask,
            ref NativeArray<SpatialQueryResult> results)
        {
            int resultCount = 0;
            float radiusSqr = radius * radius;
            int maxResults = results.Length;

            if (activeEntryCount == 0 || maxResults == 0) return 0;

            // 确定要检查的单元格范围
            float3 minPoint = center - radius;
            float3 maxPoint = center + radius;

            int minX = math.clamp((int)math.floor((minPoint.x - origin.x) / cellSize), 0, cellsX - 1);
            int minY = math.clamp((int)math.floor((minPoint.y - origin.y) / cellSize), 0, cellsY - 1);
            int minZ = math.clamp((int)math.floor((minPoint.z - origin.z) / cellSize), 0, cellsZ - 1);
            int maxX = math.clamp((int)math.floor((maxPoint.x - origin.x) / cellSize), 0, cellsX - 1);
            int maxY = math.clamp((int)math.floor((maxPoint.y - origin.y) / cellSize), 0, cellsY - 1);
            int maxZ = math.clamp((int)math.floor((maxPoint.z - origin.z) / cellSize), 0, cellsZ - 1);

            int iterationCount = 0;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        iterationCount++;
                        if (iterationCount > MAX_ITERATIONS) return resultCount;

                        int cellIdx = z * cellsY * cellsX + y * cellsX + x;
                        if (cellIdx < 0 || cellIdx >= buckets.Length) continue;

                        var bucket = buckets[cellIdx];

                        for (int i = 0; i < bucket.Count && resultCount < maxResults; i++)
                        {
                            int entryIdx = bucket[i];
                            if (entryIdx < 0 || entryIdx >= activeEntryCount) continue;

                            var entry = entries[entryIdx];

                            if (!entry.IsActive || (entry.LayerMask & layerMask) == 0) continue;

                            float distSqr = math.distancesq(center, entry.Position);
                            if (distSqr <= radiusSqr)
                            {
                                results[resultCount++] = new SpatialQueryResult
                                {
                                    EntityId = entry.EntityId,
                                    DistanceSqr = distSqr,
                                    Position = entry.Position,
                                    LayerMask = entry.LayerMask
                                };
                            }
                        }
                    }
                }
            }

            return resultCount;
        }

        /// <summary>
        /// [BurstCompile] 最近实体查询核心：在给定半径内找到最近的活跃实体。
        /// </summary>
        [BurstCompile]
        public static bool QueryNearestBurst(
            in NativeArray<SpatialCell> buckets,
            in NativeArray<SpatialEntry> entries,
            int activeEntryCount,
            int cellsX, int cellsY, int cellsZ,
            float cellSize, in float3 origin,
            in float3 center, float radius, int layerMask,
            out SpatialQueryResult result)
        {
            result = default;
            if (activeEntryCount == 0) return false;

            float bestDistSqr = radius * radius;
            bool found = false;

            float3 minPoint = center - radius;
            float3 maxPoint = center + radius;

            int minX = math.clamp((int)math.floor((minPoint.x - origin.x) / cellSize), 0, cellsX - 1);
            int minY = math.clamp((int)math.floor((minPoint.y - origin.y) / cellSize), 0, cellsY - 1);
            int minZ = math.clamp((int)math.floor((minPoint.z - origin.z) / cellSize), 0, cellsZ - 1);
            int maxX = math.clamp((int)math.floor((maxPoint.x - origin.x) / cellSize), 0, cellsX - 1);
            int maxY = math.clamp((int)math.floor((maxPoint.y - origin.y) / cellSize), 0, cellsY - 1);
            int maxZ = math.clamp((int)math.floor((maxPoint.z - origin.z) / cellSize), 0, cellsZ - 1);

            int iterationCount = 0;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        iterationCount++;
                        if (iterationCount > MAX_ITERATIONS) return found;

                        int cellIdx = z * cellsY * cellsX + y * cellsX + x;
                        if (cellIdx < 0 || cellIdx >= buckets.Length) continue;

                        var bucket = buckets[cellIdx];

                        for (int i = 0; i < bucket.Count; i++)
                        {
                            int entryIdx = bucket[i];
                            if (entryIdx < 0 || entryIdx >= activeEntryCount) continue;

                            var entry = entries[entryIdx];

                            if (!entry.IsActive || (entry.LayerMask & layerMask) == 0) continue;

                            float distSqr = math.distancesq(center, entry.Position);
                            if (distSqr < bestDistSqr)
                            {
                                bestDistSqr = distSqr;
                                result = new SpatialQueryResult
                                {
                                    EntityId = entry.EntityId,
                                    DistanceSqr = distSqr,
                                    Position = entry.Position,
                                    LayerMask = entry.LayerMask
                                };
                                found = true;
                            }
                        }
                    }
                }
            }

            return found;
        }

        #endregion

        #region Managed Compatibility Wrappers

        /// <summary>
        /// 半径查询（托管兼容包装器）。
        /// 内部使用 Burst 编译的核心，接受托管数组。
        /// </summary>
        public static int QueryRadius(
            ref SpatialGrid grid,
            Vector3 center, float radius, int layerMask,
            SpatialQueryResult[] results, int maxResults)
        {
            if (results == null || maxResults <= 0) return 0;
            if (!grid.Buckets.IsCreated || !grid.Entries.IsCreated) return 0;

            float3 f3Center = new float3(center.x, center.y, center.z);

            // 使用临时 NativeArray 接收 Burst 结果
            var nativeResults = new NativeArray<SpatialQueryResult>(maxResults, Allocator.Temp);

            int count = QueryRadiusBurst(
                grid.Buckets, grid.Entries,
                grid.ActiveEntryCount,
                grid.CellsX, grid.CellsY, grid.CellsZ,
                grid.CellSize, grid.Origin,
                f3Center, radius, layerMask,
                ref nativeResults);

            // 拷贝回托管数组
            for (int i = 0; i < count; i++)
            {
                results[i] = nativeResults[i];
            }

            nativeResults.Dispose();
            return count;
        }

        /// <summary>
        /// 最近实体查询（托管兼容包装器）。
        /// </summary>
        public static bool QueryNearest(
            ref SpatialGrid grid,
            Vector3 center, float radius, int layerMask,
            out SpatialQueryResult result)
        {
            result = default;
            if (!grid.Buckets.IsCreated || !grid.Entries.IsCreated) return false;

            float3 f3Center = new float3(center.x, center.y, center.z);

            return QueryNearestBurst(
                grid.Buckets, grid.Entries,
                grid.ActiveEntryCount,
                grid.CellsX, grid.CellsY, grid.CellsZ,
                grid.CellSize, grid.Origin,
                f3Center, radius, layerMask,
                out result);
        }

        /// <summary>
        /// 带自定义过滤器的范围查询。
        /// 注意：委托不是 Burst 兼容的，此方法始终以托管模式运行。
        /// </summary>
        public delegate bool FilterDelegate(int entityId, Vector3 position, float distanceSqr);

        public static int QueryFiltered(
            ref SpatialGrid grid,
            Vector3 center, float radius, int layerMask,
            SpatialQueryResult[] results, int maxResults,
            FilterDelegate filter)
        {
            if (results == null || maxResults <= 0 || filter == null) return 0;
            if (!grid.Buckets.IsCreated || !grid.Entries.IsCreated) return 0;

            int resultCount = 0;
            int activeEntryCount = grid.ActiveEntryCount;
            float3 f3Center = new float3(center.x, center.y, center.z);

            float3 minPoint = f3Center - radius;
            float3 maxPoint = f3Center + radius;

            grid.GetCellCoords(new Vector3(minPoint.x, minPoint.y, minPoint.z), out int minX, out int minY, out int minZ);
            grid.GetCellCoords(new Vector3(maxPoint.x, maxPoint.y, maxPoint.z), out int maxX, out int maxY, out int maxZ);

            int iterationCount = 0;
            float radiusSqr = radius * radius;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        iterationCount++;
                        if (iterationCount > MAX_ITERATIONS) return resultCount;

                        int cellIdx = z * grid.CellsY * grid.CellsX + y * grid.CellsX + x;
                        if (cellIdx < 0 || cellIdx >= grid.Buckets.Length) continue;

                        var bucket = grid.Buckets[cellIdx];

                        for (int i = 0; i < bucket.Count && resultCount < maxResults; i++)
                        {
                            int entryIdx = bucket[i];
                            if (entryIdx < 0 || entryIdx >= activeEntryCount) continue;

                            var entry = grid.Entries[entryIdx];

                            if (!entry.IsActive || (entry.LayerMask & layerMask) == 0) continue;

                            float distSqr = math.distancesq(f3Center, entry.Position);
                            if (distSqr <= radiusSqr)
                            {
                                if (filter(entry.EntityId, new Vector3(entry.Position.x, entry.Position.y, entry.Position.z), distSqr))
                                {
                                    results[resultCount++] = new SpatialQueryResult
                                    {
                                        EntityId = entry.EntityId,
                                        DistanceSqr = distSqr,
                                        Position = entry.Position,
                                        LayerMask = entry.LayerMask
                                    };
                                }
                            }
                        }
                    }
                }
            }

            return resultCount;
        }

        #endregion
    }
}
