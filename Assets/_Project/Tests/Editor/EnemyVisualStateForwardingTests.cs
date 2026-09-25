using System.Reflection;
using FlockingSystem.ECS;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

public sealed class EnemyVisualStateForwardingTests
{
    /// <summary>验证托管视觉状态会完整转发到 ECS 实例属性。</summary>
    [Test]
    public void SetVisualState_ForwardsHealthFlashDeathAndCrackState()
    {
        World world = new("EnemyVisualStateForwardingTest");
        GameObject root = new("Enemy");
        EnemyFlockBridge bridge = root.AddComponent<EnemyFlockBridge>();
        EnemyFlockRuntimeService runtime = new(null);
        Entity entity = world.EntityManager.CreateEntity(
            typeof(EnemyFlockAgent),
            typeof(EnemyFlockHealth01Property),
            typeof(EnemyFlockHitFlashProperty),
            typeof(EnemyFlockDeathProgressProperty),
            typeof(EnemyFlockCrackSeedProperty),
            typeof(EnemyFlockCrackColorProperty),
            typeof(EnemyFlockEmissionColorProperty));

        try
        {
            SetField(runtime, "_world", world);
            SetField(runtime, "_entityManager", world.EntityManager);
            SetProperty(bridge, "Runtime", runtime);
            SetProperty(bridge, "Entity", entity);

            EnemyFlockVisualState state = new()
            {
                Health01 = 0.42f,
                HitFlash = 0.8f,
                DeathProgress = 0.35f,
                CrackSeed = 0.67f,
                CrackColor = new float4(0.9f, 0.2f, 0.1f, 1f),
                EmissionColor = new float4(1f, 0.05f, 0.01f, 1f),
            };
            MethodInfo method = typeof(EnemyFlockBridge).GetMethod(
                "SetVisualState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            method.Invoke(bridge, new object[] { state });
            runtime.Tick();

            Assert.That(
                world.EntityManager.GetComponentData<EnemyFlockHealth01Property>(entity).Value,
                Is.EqualTo(0.42f).Within(0.0001f));
            Assert.That(
                world.EntityManager.GetComponentData<EnemyFlockHitFlashProperty>(entity).Value,
                Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(
                world.EntityManager.GetComponentData<EnemyFlockDeathProgressProperty>(entity).Value,
                Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(
                world.EntityManager.GetComponentData<EnemyFlockCrackSeedProperty>(entity).Value,
                Is.EqualTo(0.67f).Within(0.0001f));
        }
        finally
        {
            SetProperty<EnemyFlockRuntimeService>(bridge, "Runtime", null);
            Object.DestroyImmediate(root);
            if (world.IsCreated)
            {
                world.Dispose();
            }
        }
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"找不到字段 {name}。");
        field.SetValue(target, value);
    }

    private static void SetProperty<T>(object target, string name, T value)
    {
        FieldInfo field = target.GetType().GetField(
            $"<{name}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"找不到属性 {name} 的存储字段。");
        field.SetValue(target, value);
    }
}
