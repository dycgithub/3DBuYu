using System.Collections.Generic;
using System.Reflection;
using EnemySystem.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace FlockingSystem.Tests
{
    public sealed class EnemyFlockSpawnPositionTests
    {
        [Test]
        public void SpawnPositionProvider_DoesNotImmediatelyReuseSamePoint()
        {
            GameObject providerObject = new("EnemyFlockSpawnPositionProvider");
            GameObject firstPoint = new("FirstSpawnPoint");
            GameObject secondPoint = new("SecondSpawnPoint");

            try
            {
                SpawnPositionProvider provider = providerObject.AddComponent<SpawnPositionProvider>();
                SetPrivateField(provider, "spawnPoints", new List<Transform>
                {
                    firstPoint.transform,
                    secondPoint.transform,
                });

                Vector3 firstPosition = provider.GetSpawnPosition();
                Vector3 secondPosition = provider.GetSpawnPosition();

                Assert.That(secondPosition, Is.Not.EqualTo(firstPosition));
            }
            finally
            {
                Object.DestroyImmediate(providerObject);
                Object.DestroyImmediate(firstPoint);
                Object.DestroyImmediate(secondPoint);
            }
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
