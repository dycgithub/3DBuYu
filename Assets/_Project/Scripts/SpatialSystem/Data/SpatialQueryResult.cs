using Unity.Mathematics;

namespace SpatialSystem.Data
{
    /// <summary>
    /// 空间查询的结果条目。纯值类型，使用 float3 以支持 Burst 编译。
    /// </summary>
    public struct SpatialQueryResult
    {
        /// <summary>匹配到的实体 ID。</summary>
        public int EntityId;

        /// <summary>到查询中心点的平方距离。</summary>
        public float DistanceSqr;

        /// <summary>查询时的世界坐标（快照）。</summary>
        public float3 Position;

        /// <summary>实体的层掩码。</summary>
        public int LayerMask;

        /// <summary>实际距离（惰性计算，Burst 兼容）。</summary>
        public float Distance => math.sqrt(DistanceSqr);
    }
}
