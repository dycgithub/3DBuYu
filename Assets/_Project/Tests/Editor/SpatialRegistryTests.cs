using System.Reflection;
using Interfaces;
using NUnit.Framework;
using SpatialSystem.Bridge;
using UnityEngine;

namespace SpatialSystem.Tests
{
    /// <summary>
    /// 验证空间注册表在实体生命周期早于场景组件 Awake 时，仍能完成惰性初始化。
    /// </summary>
    public sealed class SpatialRegistryTests
    {
        [Test]
        public void Register_LazilyInitializes()
        {
            GameObject registryObject = new("SpatialRegistryTest");
            GameObject entityObject = new("SpatialEntityTest");

            try
            {
                SpatialRegistry registry = registryObject.AddComponent<SpatialRegistry>();
                registry.cellSize = 1f;
                registry.gridDimensions = new Vector3Int(2, 2, 2);
                registry.maxEntries = 4;
                registry.queryBufferSize = 4;

                FieldInfo gridField = typeof(SpatialRegistry).GetField(
                    "grid",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                gridField.SetValue(registry, default(SpatialSystem.Data.SpatialGrid));

                int entityId = registry.Register(
                    new TestDamageable(entityObject.transform),
                    0.5f,
                    SpatialRegistry.LAYER_ENEMY);

                Assert.That(entityId, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(entityObject);
                Object.DestroyImmediate(registryObject);
            }
        }

        /// <summary>空间网格对象未执行 Awake 时，查询入口也能安全完成惰性初始化。</summary>
        [Test]
        public void QueryAll_LazilyInitializes()
        {
            GameObject registryObject = new("SpatialRegistryQueryTest");

            try
            {
                SpatialRegistry registry = registryObject.AddComponent<SpatialRegistry>();
                registry.cellSize = 1f;
                registry.gridDimensions = new Vector3Int(2, 2, 2);
                registry.maxEntries = 4;
                registry.queryBufferSize = 4;

                FieldInfo gridField = typeof(SpatialRegistry).GetField(
                    "grid",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                gridField.SetValue(registry, default(SpatialSystem.Data.SpatialGrid));

                Assert.That(registry.QueryAllManaged(SpatialRegistry.LAYER_ENEMY), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(registryObject);
            }
        }

        private sealed class TestDamageable : IDamageable
        {
            public TestDamageable(Transform transform)
            {
                Transform = transform;
            }

            public Vector3 Position => Transform.position;
            public bool IsAlive => true;
            public Transform Transform { get; }
        }
    }
}
