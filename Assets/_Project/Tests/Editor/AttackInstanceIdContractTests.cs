using System.Collections.Generic;
using CombatSystem;
using NUnit.Framework;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证通用投射物入口使用统一的攻击实例编号，避免不同来源产生重复 ID。
    /// </summary>
    public sealed class AttackInstanceIdContractTests
    {
        private readonly List<Object> _temporaryObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < _temporaryObjects.Count; index++)
            {
                if (_temporaryObjects[index] != null)
                {
                    Object.DestroyImmediate(_temporaryObjects[index]);
                }
            }

            _temporaryObjects.Clear();
        }

        [Test]
        public void Launch_UsesSharedAttackInstanceIdGenerator()
        {
            ProjectileProfileSO profile = Track(
                ProjectileProfileSO.CreateRuntimeProfile("shared-id-profile"));
            var spawner = new RecordingProjectileSpawner();
            var ids = new AttackInstanceIdGenerator();
            var launcher = new ProjectileLauncher(spawner, ids);

            launcher.Launch(new ProjectileLaunchRequest(
                1,
                profile,
                Vector3.zero,
                Vector3.forward,
                1f));
            int firstId = spawner.LastData.AttackInstanceId;

            launcher.Launch(new ProjectileLaunchRequest(
                1,
                profile,
                Vector3.zero,
                Vector3.forward,
                1f));
            int secondId = spawner.LastData.AttackInstanceId;

            Assert.That(firstId, Is.GreaterThan(0));
            Assert.That(secondId, Is.EqualTo(firstId + 1));
        }

        private T Track<T>(T temporaryObject) where T : Object
        {
            _temporaryObjects.Add(temporaryObject);
            return temporaryObject;
        }

        private sealed class RecordingProjectileSpawner : IProjectileSpawner
        {
            public ProjectileData LastData { get; private set; }

            public bool TrySpawnProjectile(in ProjectileData projectileData)
            {
                LastData = projectileData;
                return true;
            }

            public bool TrySpawnProjectileBatch(
                in ProjectileData projectileData,
                int projectileCount)
            {
                LastData = projectileData;
                return true;
            }
        }
    }
}
