using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Interfaces;
using Services;
using SpatialSystem.Data;
using SpatialSystem.Query;

namespace SpatialSystem.Bridge
{
    /// <summary>
    /// 空间注册表 — 纯数据 SpatialGrid 与托管 IDamageable 世界之间的唯一桥接。
    /// 实现 <see cref="ISpatialQueryService"/> 以支持依赖注入。
    ///
    /// 内部使用 NativeArray + Burst 编译查询以获得极致性能。
    /// </summary>
    public class SpatialRegistry : MonoBehaviour, ISpatialQueryService
    {
        #region 层级位掩码定义

        public const int LAYER_BULLET  = 1 << 0;   // 1
        public const int LAYER_ENEMY   = 1 << 1;   // 2
        public const int LAYER_PLAYER  = 1 << 2;   // 4
        public const int LAYER_PICKUP  = 1 << 3;   // 8
        public const int LAYER_ALL     = ~0;

        #endregion

        #region 检视面板配置

        [Header("Grid Config")]
        [Tooltip("每个单元格的世界单位大小。越小越精确，但单元格越多。")]
        public float cellSize = 8f;

        [Tooltip("网格的世界空间中心。")]
        public Vector3 gridCenter = Vector3.zero;

        [Tooltip("每个轴上的单元格数量。")]
        public Vector3Int gridDimensions = new Vector3Int(25, 8, 25);

        [Tooltip("所有实体类型的最大同时条目数。")]
        public int maxEntries = 2048;

        [Header("Query Buffer")]
        [Tooltip("每帧可复用的查询结果缓冲区大小。")]
        public int queryBufferSize = 256;

        [Header("Gizmos 调试绘制")]
        public bool drawGridGizmos = true;
        public bool drawGridLines = true;
        public bool drawOccupiedCells = true;
        public bool drawEntityMarkers = true;
        public Color gridLineColor = new Color(0f, 0.7f, 1f, 0.15f);
        public Color occupiedCellColor = new Color(0f, 1f, 0.3f, 0.25f);
        public Color entityMarkerColor = Color.yellow;
        [Tooltip("网格线绘制步长（每 N 个单元格画一条线），越大越稀疏。")]
        public int gizmoLineStep = 2;

        #endregion

        #region 内部状态

        private SpatialGrid grid;
        private int[] entityIdToEntryIndex;      // entityId % maxEntries -> index into grid.Entries
        private Dictionary<int, IDamageable> entityLookup;  // entityId -> managed reference
        private int nextEntityId;

        // 每帧可复用的 NativeArray 查询缓冲区（避免每次查询分配）
        private NativeArray<SpatialQueryResult> queryBuffer;
        private List<IDamageable> managedResultCache;

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            InitializeGrid();
        }

        private void InitializeGrid()
        {
            grid.Initialize(
                cellSize,
                new float3(gridCenter.x, gridCenter.y, gridCenter.z),
                gridDimensions.x,
                gridDimensions.y,
                gridDimensions.z,
                maxEntries,
                Allocator.Persistent
            );

            entityIdToEntryIndex = new int[maxEntries];
            for (int i = 0; i < entityIdToEntryIndex.Length; i++)
                entityIdToEntryIndex[i] = -1;

            entityLookup = new Dictionary<int, IDamageable>(maxEntries);
            nextEntityId = 1;

            queryBuffer = new NativeArray<SpatialQueryResult>(queryBufferSize, Allocator.Persistent);
            managedResultCache = new List<IDamageable>(64);
        }

        private void Update()
        {
            ValidateEntries();
        }

        private void OnDestroy()
        {
            grid.Dispose();
            if (queryBuffer.IsCreated) queryBuffer.Dispose();
        }

        #endregion

        #region 注册 API

        /// <summary>
        /// 将 IDamageable 实体注册到空间网格中。
        /// </summary>
        /// <param name="entity">要注册的实体。</param>
        /// <param name="radius">这个实体的碰撞半径。</param>
        /// <param name="layerMask">层级位掩码（使用 LAYER_* 常量）。</param>
        /// <returns>分配的实体 ID，可用于后续 UpdatePosition 或 Unregister 调用。</returns>
        public int Register(IDamageable entity, float radius, int layerMask)
        {
            if (entity == null)
            {
                Debug.LogWarning("[SpatialRegistry] Attempted to register null entity.");
                return -1;
            }

            int entityId = nextEntityId++;
            Vector3 pos = entity.Position;
            float3 f3pos = new float3(pos.x, pos.y, pos.z);
            int entryIdx = grid.Insert(entityId, f3pos, radius, layerMask);

            if (entryIdx < 0)
            {
                nextEntityId--;
                return -1;
            }

            entityIdToEntryIndex[entityId % maxEntries] = entryIdx;
            entityLookup[entityId] = entity;

            return entityId;
        }

        /// <summary>
        /// 从空间网格注销实体。
        /// </summary>
        public void Unregister(int entityId)
        {
            if (!entityLookup.ContainsKey(entityId)) return;

            int entryIdx = entityIdToEntryIndex[entityId % maxEntries];
            if (entryIdx >= 0 && entryIdx < grid.ActiveEntryCount)
            {
                grid.Remove(entryIdx);
            }

            entityIdToEntryIndex[entityId % maxEntries] = -1;
            entityLookup.Remove(entityId);
        }

        /// <summary>
        /// 更新实体的世界坐标。
        /// 每次 Update/FixedUpdate 调用，仅在实体实际移动时。
        /// </summary>
        public void UpdatePosition(int entityId, Vector3 newPosition)
        {
            int entryIdx = entityIdToEntryIndex[entityId % maxEntries];
            if (entryIdx >= 0 && entryIdx < grid.ActiveEntryCount)
            {
                float3 f3pos = new float3(newPosition.x, newPosition.y, newPosition.z);
                grid.UpdatePosition(entryIdx, f3pos);
            }
        }

        /// <summary>
        /// 获取与实体 ID 关联的托管引用。
        /// </summary>
        public IDamageable GetEntity(int entityId)
        {
            entityLookup.TryGetValue(entityId, out var entity);
            return entity;
        }

        /// <summary>
        /// 获取实体的条目索引（用于高级/ECS 风格代码）。
        /// </summary>
        public int GetEntryIndex(int entityId)
        {
            return entityIdToEntryIndex[entityId % maxEntries];
        }

        #endregion

        #region 查询 API (返回托管对象)

        /// <summary>
        /// 半径查询：返回所有匹配的 IDamageable 托管列表。
        /// 内部使用 Burst 编译的空间查询。
        /// 返回内部缓存列表——不要持有该引用。
        /// </summary>
        public List<IDamageable> QueryRadiusManaged(Vector3 center, float radius, int layerMask)
        {
            managedResultCache.Clear();

            float3 f3Center = new float3(center.x, center.y, center.z);

            int count = SpatialQuery.QueryRadiusBurst(
                grid.Buckets, grid.Entries,
                grid.ActiveEntryCount,
                grid.CellsX, grid.CellsY, grid.CellsZ,
                grid.CellSize, grid.Origin,
                f3Center, radius, layerMask,
                ref queryBuffer);

            for (int i = 0; i < count; i++)
            {
                var result = queryBuffer[i];
                var entity = GetEntity(result.EntityId);
                if (entity != null && entity.IsAlive && entity.Transform != null)
                {
                    managedResultCache.Add(entity);
                }
            }

            return managedResultCache;
        }

        /// <summary>
        /// 最近实体查询：返回最近的匹配 IDamageable，或 null。
        /// 内部使用 Burst 编译的空间查询。
        /// </summary>
        public IDamageable QueryNearestManaged(Vector3 center, float radius, int layerMask)
        {
            float3 f3Center = new float3(center.x, center.y, center.z);

            if (SpatialQuery.QueryNearestBurst(
                grid.Buckets, grid.Entries,
                grid.ActiveEntryCount,
                grid.CellsX, grid.CellsY, grid.CellsZ,
                grid.CellSize, grid.Origin,
                f3Center, radius, layerMask,
                out var result))
            {
                var entity = GetEntity(result.EntityId);
                if (entity != null && entity.IsAlive && entity.Transform != null)
                {
                    return entity;
                }
            }
            return null;
        }

        /// <summary>
        /// 带自定义过滤委托的半径查询。
        /// 注意：委托不兼容 Burst；此方法以托管模式运行。
        /// </summary>
        public List<IDamageable> QueryFilteredManaged(Vector3 center, float radius, int layerMask,
            SpatialQuery.FilterDelegate filter)
        {
            managedResultCache.Clear();

            var managedBuffer = new SpatialQueryResult[queryBufferSize];
            int count = SpatialQuery.QueryFiltered(ref grid, center, radius, layerMask, managedBuffer, queryBufferSize, filter);

            for (int i = 0; i < count; i++)
            {
                var result = managedBuffer[i];
                var entity = GetEntity(result.EntityId);
                if (entity != null && entity.IsAlive && entity.Transform != null)
                {
                    managedResultCache.Add(entity);
                }
            }

            return managedResultCache;
        }

        /// <summary>
        /// 获取原始查询结果（非托管包装）。用于需要对结果排序/过滤的代码。
        /// 返回内部缓冲区——请立即复制，下次查询时将被覆盖。
        /// </summary>
        public SpatialQueryResult[] QueryRaw(Vector3 center, float radius, int layerMask, out int resultCount)
        {
            float3 f3Center = new float3(center.x, center.y, center.z);

            resultCount = SpatialQuery.QueryRadiusBurst(
                grid.Buckets, grid.Entries,
                grid.ActiveEntryCount,
                grid.CellsX, grid.CellsY, grid.CellsZ,
                grid.CellSize, grid.Origin,
                f3Center, radius, layerMask,
                ref queryBuffer);

            // 将 NativeArray 转换为托管数组供调用者使用
            var managed = new SpatialQueryResult[resultCount];
            for (int i = 0; i < resultCount; i++)
            {
                managed[i] = queryBuffer[i];
            }
            return managed;
        }

        #endregion

        #region 维护

        /// <summary>
        /// 验证并清理过期条目（每帧调用）。
        /// 移除 GameObject 为 null 或 IsAlive 为 false 的实体。
        /// </summary>
        private void ValidateEntries()
        {
            var expiredIds = new List<int>();

            foreach (var kvp in entityLookup)
            {
                var entity = kvp.Value;
                if (entity == null || entity.Transform == null || !entity.IsAlive)
                {
                    expiredIds.Add(kvp.Key);
                }
            }

            foreach (int id in expiredIds)
            {
                Unregister(id);
            }

            if (expiredIds.Count > 0)
            {
                CompactIfNeeded();
            }
        }

        /// <summary>
        /// 如果空洞过多则压缩条目数组。
        /// 活跃实体的 EntryIndex 会改变，entityIdToEntryIndex 也会相应更新。
        /// </summary>
        private void CompactIfNeeded()
        {
            int deadCount = 0;
            for (int i = 0; i < grid.ActiveEntryCount; i++)
            {
                if (!grid.Entries[i].IsActive) deadCount++;
            }

            // 如果死亡条目超过 25% 或活跃条目不足容量一半，则压缩
            if (deadCount > grid.ActiveEntryCount * 0.25f || grid.ActiveEntryCount < maxEntries * 0.5f)
            {
                Compact();
            }
        }

        private void Compact()
        {
            // 重建网格——2000 条目足够快 (<1ms)
            var oldLookup = new Dictionary<int, (IDamageable entity, float radius, int layerMask)>();
            foreach (var kvp in entityLookup)
            {
                if (kvp.Value != null && kvp.Value.IsAlive && kvp.Value.Transform != null)
                {
                    int entryIdx = entityIdToEntryIndex[kvp.Key % maxEntries];
                    if (entryIdx >= 0 && entryIdx < grid.ActiveEntryCount)
                    {
                        var entry = grid.Entries[entryIdx];
                        if (entry.IsActive)
                        {
                            oldLookup[kvp.Key] = (kvp.Value, entry.Radius, entry.LayerMask);
                        }
                    }
                }
            }

            // 释放旧网格并重新初始化
            grid.Dispose();
            InitializeGrid();

            foreach (var kvp in oldLookup)
            {
                int newId = kvp.Key;
                var (entity, radius, mask) = kvp.Value;
                Vector3 pos = entity.Position;
                float3 f3pos = new float3(pos.x, pos.y, pos.z);
                int entryIdx = grid.Insert(newId, f3pos, radius, mask);
                if (entryIdx >= 0)
                {
                    entityIdToEntryIndex[newId % maxEntries] = entryIdx;
                    entityLookup[newId] = entity;
                }
            }
        }

        #endregion

        #region ISpatialQueryService 显式实现

        IDamageable ISpatialQueryService.QueryNearest(Vector3 center, float radius, int layerMask)
            => QueryNearestManaged(center, radius, layerMask);

        List<IDamageable> ISpatialQueryService.QueryRadius(Vector3 center, float radius, int layerMask)
            => QueryRadiusManaged(center, radius, layerMask);

        #endregion

#if UNITY_EDITOR
        #region Gizmos 绘制

        private void OnDrawGizmosSelected()
        {
            if (!drawGridGizmos) return;
            if (!grid.Buckets.IsCreated) return;

            Vector3 origin = grid.GridMinV3;
            Vector3 max = grid.GridMaxV3;
            Vector3 size = grid.GridSizeV3;
            Vector3 center = (origin + max) * 0.5f;

            // 外框
            Gizmos.color = gridLineColor;
            Gizmos.color *= 2f;
            Gizmos.DrawWireCube(center, size);

            if (drawGridLines)
                DrawGridGizmoLines(origin, max);

            if (drawOccupiedCells)
                DrawOccupiedGizmoCells(origin);

            if (drawEntityMarkers)
                DrawEntityGizmoMarkers();
        }

        private void DrawGridGizmoLines(Vector3 origin, Vector3 max)
        {
            Gizmos.color = gridLineColor;
            float cs = cellSize;
            int step = Mathf.Max(1, gizmoLineStep);

            for (int iy = 0; iy <= grid.CellsY; iy += step)
            {
                for (int iz = 0; iz <= grid.CellsZ; iz += step)
                {
                    Vector3 start = new Vector3(origin.x, origin.y + iy * cs, origin.z + iz * cs);
                    Vector3 end   = new Vector3(max.x,    origin.y + iy * cs, origin.z + iz * cs);
                    Gizmos.DrawLine(start, end);
                }
            }

            for (int ix = 0; ix <= grid.CellsX; ix += step)
            {
                for (int iz = 0; iz <= grid.CellsZ; iz += step)
                {
                    Vector3 start = new Vector3(origin.x + ix * cs, origin.y,          origin.z + iz * cs);
                    Vector3 end   = new Vector3(origin.x + ix * cs, max.y,             origin.z + iz * cs);
                    Gizmos.DrawLine(start, end);
                }
            }

            for (int ix = 0; ix <= grid.CellsX; ix += step)
            {
                for (int iy = 0; iy <= grid.CellsY; iy += step)
                {
                    Vector3 start = new Vector3(origin.x + ix * cs, origin.y + iy * cs, origin.z);
                    Vector3 end   = new Vector3(origin.x + ix * cs, origin.y + iy * cs, max.z);
                    Gizmos.DrawLine(start, end);
                }
            }
        }

        private void DrawOccupiedGizmoCells(Vector3 origin)
        {
            Gizmos.color = occupiedCellColor;
            float cs = cellSize;

            for (int cellIdx = 0; cellIdx < grid.Buckets.Length; cellIdx++)
            {
                var cell = grid.Buckets[cellIdx];
                if (cell.Count == 0) continue;

                int cx = cellIdx % grid.CellsX;
                int cy = (cellIdx / grid.CellsX) % grid.CellsY;
                int cz = cellIdx / (grid.CellsX * grid.CellsY);

                Vector3 cellCenter = new Vector3(
                    origin.x + (cx + 0.5f) * cs,
                    origin.y + (cy + 0.5f) * cs,
                    origin.z + (cz + 0.5f) * cs
                );
                Vector3 cellSize = Vector3.one * cs;
                Gizmos.DrawWireCube(cellCenter, cellSize);
            }
        }

        private void DrawEntityGizmoMarkers()
        {
            Gizmos.color = entityMarkerColor;
            for (int i = 0; i < grid.ActiveEntryCount; i++)
            {
                var entry = grid.Entries[i];
                if (!entry.IsActive) continue;

                Vector3 pos = new Vector3(entry.Position.x, entry.Position.y, entry.Position.z);
                float r = entry.Radius;
                Gizmos.DrawWireSphere(pos, r * 0.5f);
                Gizmos.DrawLine(pos + Vector3.left   * r * 0.4f, pos + Vector3.right  * r * 0.4f);
                Gizmos.DrawLine(pos + Vector3.up     * r * 0.4f, pos + Vector3.down   * r * 0.4f);
            }
        }

        #endregion
#endif
    }
}
