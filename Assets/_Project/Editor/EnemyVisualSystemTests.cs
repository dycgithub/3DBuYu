using System;
using System.Reflection;
using EnemySystem;
using EnemySystem.Visual;
using FlockingSystem.ECS;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyVisualSystemTests
{
    private const BindingFlags InstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private EnemyAttributesSO _attributes;
    private GameObject _enemyObject;

    /// <summary>Releases temporary test objects without changing the shared config asset.</summary>
    [TearDown]
    public void TearDown()
    {
        if (_enemyObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_enemyObject);
        }

        if (_attributes != null)
        {
            UnityEngine.Object.DestroyImmediate(_attributes);
        }
    }

    /// <summary>Shared feedback settings must preserve the type material's cached base color.</summary>
    [Test]
    public void Initialize_PreservesTypeColor()
    {
        EnemyVisualConfigSO sharedConfig = AssetDatabase.LoadAssetAtPath<EnemyVisualConfigSO>(
            "Assets/_Project/SO/Enemy/EnemyVisualConfig.asset");
        Assert.That(sharedConfig, Is.Not.Null);

        _attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
        _attributes.visualConfig = sharedConfig;

        _enemyObject = new GameObject("EnemyVisualSystemTest");
        _enemyObject.SetActive(false);
        Enemy enemy = _enemyObject.AddComponent<Enemy>();
        EnemyFlockBridge bridge = _enemyObject.GetComponent<EnemyFlockBridge>();
        SetField(typeof(Enemy), enemy, "enemyAttributes", _attributes);
        SetField(
            typeof(EnemyFlockBridge),
            bridge,
            "_baseColor",
            CreateColor(0.2f, 0.4f, 0.8f, 1f));

        Type entryType = typeof(EnemyVisualSystem).GetNestedType("Entry", BindingFlags.NonPublic);
        object entry = Activator.CreateInstance(entryType, true);
        SetField(entryType, entry, "Enemy", enemy);
        SetField(entryType, entry, "Bridge", bridge);
        SetField(entryType, entry, "State", Activator.CreateInstance(typeof(EnemyFlockVisualState)));

        MethodInfo initialize = typeof(EnemyVisualSystem).GetMethod(
            "InitializeBoundState",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(initialize, Is.Not.Null);
        initialize.Invoke(null, new[] { entry });

        object state = GetField(entryType, "State").GetValue(entry);
        object baseColor = GetField(state.GetType(), "BaseColor").GetValue(state);
        Assert.That(ReadColorComponent(baseColor, "x"), Is.EqualTo(0.2f));
        Assert.That(ReadColorComponent(baseColor, "y"), Is.EqualTo(0.4f));
        Assert.That(ReadColorComponent(baseColor, "z"), Is.EqualTo(0.8f));
    }

    /// <summary>Sets a private serialized or nested-entry field for this narrow regression test.</summary>
    private static void SetField(Type ownerType, object target, string name, object value)
    {
        GetField(ownerType, name).SetValue(target, value);
    }

    /// <summary>Finds a field needed to arrange or observe the initialization seam.</summary>
    private static FieldInfo GetField(Type ownerType, string name)
    {
        FieldInfo field = ownerType.GetField(name, InstanceFields);
        Assert.That(field, Is.Not.Null, $"Missing test field: {ownerType.FullName}.{name}");
        return field;
    }

    /// <summary>Creates a Mathematics color value without exposing it in the test assembly API.</summary>
    private static object CreateColor(float red, float green, float blue, float alpha)
    {
        Type colorType = Type.GetType("Unity.Mathematics.float4, Unity.Mathematics", true);
        return Activator.CreateInstance(colorType, new object[] { red, green, blue, alpha });
    }

    /// <summary>Reads one channel from the cached ECS material color.</summary>
    private static float ReadColorComponent(object color, string channel)
    {
        return (float)GetField(color.GetType(), channel).GetValue(color);
    }
}
