using System;
using System.Reflection;
using FlockingSystem.ECS;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace FlockingSystem.Tests
{
    /// <summary>
    /// 验证 Flocking EditorWindow 的范围换算和 ECS 活跃代理统计规则。
    /// </summary>
    public sealed class FlockingWindowTests
    {
        [Test]
        public void CalculateSwimBounds_UsesWorldCenterAndDoubleHalfExtents()
        {
            MethodInfo method = GetPrivateStaticMethod("CalculateSwimBounds");
            Vector3 center = new(3f, 4f, 5f);
            Vector3 halfExtents = new(10f, 20f, 30f);

            Bounds bounds = (Bounds)method.Invoke(null, new object[] { center, halfExtents });

            Assert.That(bounds.center, Is.EqualTo(center));
            Assert.That(bounds.size, Is.EqualTo(new Vector3(20f, 40f, 60f)));
        }

        [Test]
        public void SwimDrawing_ToggleWorks()
        {
            FieldInfo drawField = GetPrivateField("_drawSwimBounds");
            FieldInfo settingsField = GetPrivateField("_flockSettings");
            MethodInfo method = GetPrivateMethod("ShouldDrawSwimBounds");
            FlockingWindow window = ScriptableObject.CreateInstance<FlockingWindow>();
            EnemyFlockSettingsSO settings = ScriptableObject.CreateInstance<EnemyFlockSettingsSO>();

            try
            {
                Assert.That((bool)drawField.GetValue(window), Is.True);

                settingsField.SetValue(window, settings);
                Assert.That((bool)method.Invoke(window, null), Is.True);

                drawField.SetValue(window, false);
                Assert.That((bool)method.Invoke(window, null), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CountActiveAgents_ExcludesDisabledFlockingAgents()
        {
            MethodInfo method = GetPrivateStaticMethod("CountActiveAgents");
            using World world = new("FlockingWindowTests");
            EntityManager entityManager = world.EntityManager;
            EntityArchetype archetype = entityManager.CreateArchetype(
                typeof(EnemyFlockAgent),
                typeof(EnemyFlockActive));
            using NativeArray<Entity> entities = new(3, Allocator.Temp);

            entityManager.CreateEntity(archetype, entities);
            entityManager.SetComponentEnabled<EnemyFlockActive>(entities[1], false);

            int count = (int)method.Invoke(null, new object[] { world });

            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void CountActiveAgents_ReturnsUnavailableForMissingWorld()
        {
            MethodInfo method = GetPrivateStaticMethod("CountActiveAgents");

            int count = (int)method.Invoke(null, new object[] { null });

            Assert.That(count, Is.EqualTo(-1));
        }

        private static MethodInfo GetPrivateStaticMethod(string methodName)
        {
            MethodInfo method = typeof(FlockingWindow).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到 FlockingWindow.{methodName}。\n");
            return method;
        }

        private static MethodInfo GetPrivateMethod(string methodName)
        {
            MethodInfo method = typeof(FlockingWindow).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到 FlockingWindow.{methodName}。\n");
            return method;
        }

        private static FieldInfo GetPrivateField(string fieldName)
        {
            FieldInfo field = typeof(FlockingWindow).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"未找到 FlockingWindow.{fieldName}。\n");
            return field;
        }
    }
}
