using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using SpatialSystem.Data;

namespace SpatialSystem.Tests
{
    /// <summary>
    /// 验证稀疏网格按几何中心初始化时，边界关于指定中心对称。
    /// </summary>
    public sealed class SpatialGridTests
    {
        [Test]
        public void InitializeCentered_PlacesGeometryCenterAtOrigin()
        {
            SpatialGrid grid = new();
            try
            {
                grid.InitializeCentered(
                    cellSize: 1.5f,
                    center: float3.zero,
                    cellsX: 4,
                    cellsY: 2,
                    cellsZ: 6,
                    maxEntries: 1,
                    allocator: Allocator.Temp);

                Assert.That(grid.GridMin, Is.EqualTo(new float3(-3f, -1.5f, -4.5f)));
                Assert.That(grid.GridMax, Is.EqualTo(new float3(3f, 1.5f, 4.5f)));
                Assert.That((grid.GridMin + grid.GridMax) * 0.5f, Is.EqualTo(float3.zero));
            }
            finally
            {
                grid.Dispose();
            }
        }
    }
}
