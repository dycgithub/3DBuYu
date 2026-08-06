using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace SpatialSystem.Data
{
    /// <summary>
    /// 纯数据的空间网格（DOTS/Burst 兼容）。
    /// 使用 NativeArray 替代托管数组，float3 替代 Vector3。
    /// 可被 Burst-compiled 方法直接使用。
    ///
    /// 网格将 3D 空间划分为均匀单元格。每个实体根据其位置
    /// 被分配到对应单元格。查询时只检查相关单元格，而不是所有实体。
    /// </summary>
    public struct SpatialGrid
    {
        /// <summary>每个单元格的世界单位大小。</summary>
        public float CellSize;

        /// <summary>各轴上的单元格数量。</summary>
        public int CellsX, CellsY, CellsZ;

        /// <summary>网格的世界空间原点（Burst 兼容的 float3）。</summary>
        public float3 Origin;

        /// <summary>所有桶的 NativeArray（每单元格一个）。需手动释放。</summary>
        public NativeArray<SpatialCell> Buckets;

        /// <summary>所有条目的 NativeArray。需手动释放。</summary>
        public NativeArray<SpatialEntry> Entries;

        /// <summary>全局条目容量上限。</summary>
        public int MaxEntriesTotal;

        /// <summary>活跃条目数量。</summary>
        public int ActiveEntryCount;

        /// <summary>网格的边界。</summary>
        public float3 GridMin => Origin;
        public float3 GridMax => Origin + new float3(CellsX * CellSize, CellsY * CellSize, CellsZ * CellSize);
        public float3 GridSize => new float3(CellsX * CellSize, CellsY * CellSize, CellsZ * CellSize);

        /// <summary>Vector3 兼容的边界属性（用于托管代码调试/可视化）。</summary>
        public Vector3 GridMinV3 => new Vector3(Origin.x, Origin.y, Origin.z);
        public Vector3 GridMaxV3 => new Vector3(Origin.x + CellsX * CellSize, Origin.y + CellsY * CellSize, Origin.z + CellsZ * CellSize);
        public Vector3 GridSizeV3 => new Vector3(CellsX * CellSize, CellsY * CellSize, CellsZ * CellSize);

        /// <summary>
        /// 分配 NativeArray 并初始化网格。
        /// </summary>
        public void Initialize(float cellSize, float3 origin, int cellsX, int cellsY, int cellsZ, int maxEntries, Allocator allocator)
        {
            CellSize = cellSize;
            Origin = origin;
            CellsX = cellsX;
            CellsY = cellsY;
            CellsZ = cellsZ;
            MaxEntriesTotal = maxEntries;
            ActiveEntryCount = 0;

            int totalCells = cellsX * cellsY * cellsZ;
            Buckets = new NativeArray<SpatialCell>(totalCells, allocator);
            Entries = new NativeArray<SpatialEntry>(maxEntries, allocator);
        }

        /// <summary>
        /// 释放 NativeArray 内存。使用完毕后必须调用。
        /// </summary>
        public void Dispose()
        {
            if (Buckets.IsCreated) Buckets.Dispose();
            if (Entries.IsCreated) Entries.Dispose();
        }

        /// <summary>
        /// 计算给定世界坐标的扁平数组索引。
        /// 越界坐标会被钳制到有效范围内。
        /// </summary>
        public int ComputeCellIndex(float3 position)
        {
            int x = math.clamp((int)math.floor((position.x - Origin.x) / CellSize), 0, CellsX - 1);
            int y = math.clamp((int)math.floor((position.y - Origin.y) / CellSize), 0, CellsY - 1);
            int z = math.clamp((int)math.floor((position.z - Origin.z) / CellSize), 0, CellsZ - 1);
            return z * CellsY * CellsX + y * CellsX + x;
        }

        /// <summary>
        /// 获取世界坐标所在的单元格的 X/Y/Z 索引。
        /// 越界坐标会被钳制。
        /// </summary>
        public void GetCellCoords(float3 position, out int cx, out int cy, out int cz)
        {
            cx = math.clamp((int)math.floor((position.x - Origin.x) / CellSize), 0, CellsX - 1);
            cy = math.clamp((int)math.floor((position.y - Origin.y) / CellSize), 0, CellsY - 1);
            cz = math.clamp((int)math.floor((position.z - Origin.z) / CellSize), 0, CellsZ - 1);
        }

        /// <summary>
        /// 插入一个实体到网格中。返回条目索引，如果网格已满则返回 -1。
        /// </summary>
        public int Insert(int entityId, float3 position, float radius, int layerMask)
        {
            if (ActiveEntryCount >= MaxEntriesTotal)
            {
                Debug.LogWarning($"[SpatialGrid] 网格已满 ({ActiveEntryCount}/{MaxEntriesTotal})，无法插入实体 {entityId}。");
                return -1;
            }

            int cellIdx = ComputeCellIndex(position);
            var bucket = Buckets[cellIdx];

            if (!bucket.Add(ActiveEntryCount))
            {
                Debug.LogWarning($"[SpatialGrid] 单元格 {cellIdx} 已满 ({SpatialCell.MaxEntries})。");
            }

            Buckets[cellIdx] = bucket;

            Entries[ActiveEntryCount] = new SpatialEntry
            {
                EntityId = entityId,
                Position = position,
                Radius = radius,
                LayerMask = layerMask,
                IsActive = true
            };

            return ActiveEntryCount++;
        }

        /// <summary>
        /// 从网格中移除一个条目。
        /// </summary>
        public void Remove(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= ActiveEntryCount) return;
            var entry = Entries[entryIndex];
            if (!entry.IsActive) return;

            entry.IsActive = false;
            Entries[entryIndex] = entry;

            int cellIdx = ComputeCellIndex(entry.Position);
            var bucket = Buckets[cellIdx];
            bucket.Remove(entryIndex);
            Buckets[cellIdx] = bucket;
        }

        /// <summary>
        /// 更新条目位置（先移除旧的桶引用，再加入新的）。
        /// </summary>
        public void UpdatePosition(int entryIndex, float3 newPosition)
        {
            if (entryIndex < 0 || entryIndex >= ActiveEntryCount) return;
            var entry = Entries[entryIndex];
            if (!entry.IsActive) return;

            int oldCell = ComputeCellIndex(entry.Position);
            int newCell = ComputeCellIndex(newPosition);

            entry.Position = newPosition;
            Entries[entryIndex] = entry;

            if (oldCell != newCell)
            {
                var oldBucket = Buckets[oldCell];
                oldBucket.Remove(entryIndex);
                Buckets[oldCell] = oldBucket;

                var newBucket = Buckets[newCell];
                if (newBucket.Add(entryIndex))
                {
                    Buckets[newCell] = newBucket;
                }
            }
        }

        /// <summary>
        /// 清空所有桶和条目。
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < Buckets.Length; i++)
            {
                var b = Buckets[i];
                b.Clear();
                Buckets[i] = b;
            }
            for (int i = 0; i < Entries.Length; i++)
            {
                Entries[i] = default;
            }
            ActiveEntryCount = 0;
        }

        #region Managed Compatibility (Vector3 overloads)

        /// <summary>
        /// Vector3 兼容的初始化（供 SpatialRegistry 使用）。
        /// </summary>
        public void Initialize(float cellSize, Vector3 origin, int cellsX, int cellsY, int cellsZ, int maxEntries, Allocator allocator)
        {
            Initialize(cellSize, new float3(origin.x, origin.y, origin.z), cellsX, cellsY, cellsZ, maxEntries, allocator);
        }

        /// <summary>
        /// Vector3 兼容的单元格索引计算。
        /// </summary>
        public int ComputeCellIndex(Vector3 position)
        {
            return ComputeCellIndex(new float3(position.x, position.y, position.z));
        }

        /// <summary>
        /// Vector3 兼容的单元格坐标获取。
        /// </summary>
        public void GetCellCoords(Vector3 position, out int cx, out int cy, out int cz)
        {
            GetCellCoords(new float3(position.x, position.y, position.z), out cx, out cy, out cz);
        }

        /// <summary>
        /// Vector3 兼容的插入。
        /// </summary>
        public int Insert(int entityId, Vector3 position, float radius, int layerMask)
        {
            return Insert(entityId, new float3(position.x, position.y, position.z), radius, layerMask);
        }

        /// <summary>
        /// Vector3 兼容的位置更新。
        /// </summary>
        public void UpdatePosition(int entryIndex, Vector3 newPosition)
        {
            UpdatePosition(entryIndex, new float3(newPosition.x, newPosition.y, newPosition.z));
        }

        #endregion
    }
}
