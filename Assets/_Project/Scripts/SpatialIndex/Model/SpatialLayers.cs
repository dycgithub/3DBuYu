namespace SpatialIndexing
{
    /// <summary>伤害目标类别掩码，与 Unity 物理层分离。</summary>
    public static class SpatialLayers
    {
        public const int Enemy = 1 << 1;
        public const int Player = 1 << 2;
    }
}
