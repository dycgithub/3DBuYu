using Unity.Mathematics;

namespace SpatialSystem.Data
{
    /// <summary>
    /// 空间网格中的单个条目。
    /// 纯值类型数据，使用 float3 以支持 Burst 编译。
    /// </summary>
    public struct SpatialEntry
    {
        /// <summary>实体的唯一 ID（通过 SpatialRegistry 映射回 IDamageable）。</summary>
        public int EntityId;

        /// <summary>当前世界坐标（Burst 兼容的 float3）。</summary>
        public float3 Position;

        /// <summary>边界半径，用于邻近检测。</summary>
        public float Radius;

        /// <summary>类别位掩码。</summary>
        public int LayerMask;

        /// <summary>此条目当前是否活跃。</summary>
        public bool IsActive;
    }
}
