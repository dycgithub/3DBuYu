using EnemySystem.Spawning;
using NUnit.Framework;
using UnityEngine;
using Utils;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证敌人对象池在没有 VContainer 解析器时仍能通过 Unity 实例化并完成回收。
    /// </summary>
    public sealed class EnemyPoolResolverFallbackTests
    {
        [Test]
        public void EnemyPool_UsesUnityInstantiationWhenResolverIsMissing()
        {
            GameObject prefab = new("EnemyPoolFallbackPrefab");
            GameObjectPoolService sharedPool = new();
            EnemyPool enemyPool = new(sharedPool, null);

            try
            {
                GameObject instance = enemyPool.RentEnemy(prefab);

                Assert.That(instance, Is.Not.Null);
                Assert.That(instance, Is.Not.SameAs(prefab));
                Assert.That(instance.activeSelf, Is.False);

                PoolUsage rentedUsage = sharedPool.GetUsage(prefab);
                Assert.That(rentedUsage.RentedCount, Is.EqualTo(1));

                enemyPool.ReturnEnemy(instance);

                PoolUsage returnedUsage = sharedPool.GetUsage(prefab);
                Assert.That(returnedUsage.RentedCount, Is.Zero);
                Assert.That(returnedUsage.AvailableCount, Is.EqualTo(10));
            }
            finally
            {
                enemyPool.Dispose();
                Object.DestroyImmediate(prefab);
            }
        }
    }
}
