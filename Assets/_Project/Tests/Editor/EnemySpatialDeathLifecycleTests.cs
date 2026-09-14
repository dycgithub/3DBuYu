using System.Collections.Generic;
using System.Reflection;
using CombatSystem;
using EnemySystem;
using FlockingSystem.ECS;
using Interfaces;
using NUnit.Framework;
using Services;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证敌人开始死亡时立即退出空间查询，避免死亡高亮期间阻塞最近目标解析。
    /// </summary>
    public sealed class EnemySpatialDeathLifecycleTests
    {
        [Test]
        public void DeathStarted_UnregistersEnemyBeforeDelayedPoolRelease()
        {
            GameObject enemyObject = new("EnemySpatialDeathLifecycleTest");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
            EnemyFlockSettingsSO flockSettings = ScriptableObject.CreateInstance<EnemyFlockSettingsSO>();
            RecordingSpatialService spatialService = new();

            try
            {
                enemyObject.SetActive(false);
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                attributes.baseHealth = 10f;
                SetField(enemy, "enemyAttributes", attributes);
                SetField(enemy, "_spatialService", spatialService);
                SetField(enemy, "_flockSettings", flockSettings);

                enemyObject.SetActive(true);
                enemy.InitializeForSpawn(1f, 1f);
                Assert.That(spatialService.RegisterCalls, Is.EqualTo(1));

                enemy.ReceiveDamageRequest(new DamageRequest
                {
                    DamageAmount = 10f,
                    DamageType = DamageType.Physical
                });

                Assert.That(spatialService.UnregisterCalls, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
                Object.DestroyImmediate(attributes);
                Object.DestroyImmediate(flockSettings);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private sealed class RecordingSpatialService : ISpatialQueryService
        {
            public int RegisterCalls { get; private set; }
            public int UnregisterCalls { get; private set; }

            public int Register(IDamageable entity, float radius, int layerMask)
            {
                RegisterCalls++;
                return RegisterCalls;
            }

            public void Unregister(int entityId)
            {
                UnregisterCalls++;
            }

            public void UpdatePosition(int entityId, Vector3 position)
            {
            }

            public IDamageable QueryNearest(Vector3 center, float radius, int layerMask)
            {
                return null;
            }

            public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask)
            {
                return new List<IDamageable>();
            }

            public List<IDamageable> QueryAll(int layerMask)
            {
                return new List<IDamageable>();
            }
        }
    }
}
