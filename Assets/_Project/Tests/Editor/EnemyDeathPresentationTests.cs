using System.IO;
using System.Reflection;
using EnemySystem;
using EnemySystem.Components;
using FlockingSystem.ECS;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using Unity.Rendering;
using UnityEngine;

public sealed class EnemyDeathPresentationTests
{
    [Test]
    public void DeathKeepsSourceRendererHidden()
    {
        World world = new("EnemyDeathPresentationTest");
        GameObject root = new("Enemy");
        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.transform.SetParent(root.transform, false);
        MeshRenderer renderer = model.GetComponent<MeshRenderer>();
        EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
        EnemyFlockRuntimeService runtime = new(null);
        EnemyFlockBridge bridge = null;

        try
        {
            root.SetActive(false);
            bridge = root.AddComponent<EnemyFlockBridge>();
            root.AddComponent<EnemyHealthComponent>();
            root.AddComponent<EnemyDamagePresentationComponent>();
            Enemy enemy = root.AddComponent<Enemy>();
            attributes.baseHealth = 1f;
            SetField(enemy, "enemyAttributes", attributes);

            root.SetActive(true);
            bridge.SetEcsPresentationEnabled(true);

            SetField(runtime, "_world", world);
            SetField(runtime, "_entityManager", world.EntityManager);
            SetField(
                runtime,
                "_commitSystem",
                world.GetOrCreateSystemManaged<EnemyFlockCommitSystem>());

            Entity entity = world.EntityManager.CreateEntity();
            SetProperty(bridge, "Runtime", runtime);
            SetProperty(bridge, "Entity", entity);
            SetProperty(bridge, "Slot", 0);
            SetProperty(bridge, "TransformArrayIndex", -1);

            Assert.That(renderer.enabled, Is.False);

            enemy.Kill();

            Assert.That(renderer.enabled, Is.False);
        }
        finally
        {
            if (bridge != null)
            {
                SetProperty<EnemyFlockRuntimeService>(bridge, "Runtime", null);
            }

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(attributes);
            if (world.IsCreated)
            {
                world.Dispose();
            }
        }
    }

    /// <summary>验证运行时 ECS 材质优先使用 EnemyFlockToon。</summary>
    [Test]
    public void CreatesFlockMaterial_UsesEnemyFlockToonShader()
    {
        Shader toonShader = Shader.Find("Custom/EnemyFlockToon");
        Shader sourceShader = Shader.Find("Unlit/Color");
        Assert.That(toonShader, Is.Not.Null);
        Assert.That(sourceShader, Is.Not.Null);

        Material source = new(sourceShader);
        EnemyFlockRuntimeService runtime = new(null);
        Material result = null;

        try
        {
            MethodInfo method = typeof(EnemyFlockRuntimeService).GetMethod(
                "CreateFlockRenderMaterial",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            result = method.Invoke(runtime, new object[] { source, 0 }) as Material;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.shader, Is.EqualTo(toonShader));
        }
        finally
        {
            Object.DestroyImmediate(result);
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void CreatesFlockMaterialFromLegacyColor()
    {
        Shader sourceShader = Shader.Find("Unlit/Color");
        Assert.That(sourceShader, Is.Not.Null);

        Material source = new(sourceShader);
        EnemyFlockRuntimeService runtime = new(null);
        Material result = null;

        try
        {
            Assert.That(source.HasProperty("_Color"), Is.True);
            source.SetColor("_Color", Color.red);

            MethodInfo method = typeof(EnemyFlockRuntimeService).GetMethod(
                "CreateFlockRenderMaterial",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            result = method.Invoke(runtime, new object[] { source, 0 }) as Material;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.HasProperty("_BaseColor"), Is.True);
            Assert.That(result.GetColor("_BaseColor"), Is.EqualTo(Color.red));
        }
        finally
        {
            Object.DestroyImmediate(result);
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void CreatesFlockMaterialAndLinearEcsColorFromLegacyColor()
    {
        Shader sourceShader = Shader.Find("Unlit/Color");
        Assert.That(sourceShader, Is.Not.Null);

        Material source = new(sourceShader);
        EnemyFlockRuntimeService runtime = new(null);
        Material result = null;
        Color sourceColor = new(0.5f, 0.25f, 0.75f, 1f);

        try
        {
            source.SetColor("_Color", sourceColor);
            MethodInfo createMethod = typeof(EnemyFlockRuntimeService).GetMethod(
                "CreateFlockRenderMaterial",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo linearMethod = typeof(EnemyFlockRuntimeService).GetMethod(
                "ToLinearColor",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(Color) },
                null);
            Assert.That(createMethod, Is.Not.Null);
            Assert.That(linearMethod, Is.Not.Null);

            result = createMethod.Invoke(runtime, new object[] { source, 0 }) as Material;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.GetColor("_BaseColor"), Is.EqualTo(sourceColor));

            float4 linear = (float4)linearMethod.Invoke(null, new object[] { sourceColor });
            Color expected = sourceColor.linear;
            Assert.That(linear.x, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(linear.y, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(linear.z, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(linear.w, Is.EqualTo(expected.a).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(result);
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void VisualCacheKeySeparatesSourcePrefabAndVisualIndex()
    {
        GameObject firstSource = new("FirstSource");
        GameObject secondSource = new("SecondSource");

        try
        {
            var keyType = typeof(EnemyFlockRuntimeService).GetNestedType(
                "EnemyFlockVisualKey",
                BindingFlags.NonPublic);
            Assert.That(keyType, Is.Not.Null);

            ConstructorInfo constructor = keyType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(GameObject), typeof(int) },
                null);
            MethodInfo equalsMethod = keyType.GetMethod(
                "Equals",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { keyType },
                null);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(equalsMethod, Is.Not.Null);

            object firstIndexZero = constructor.Invoke(new object[] { firstSource, 0 });
            object firstIndexOne = constructor.Invoke(new object[] { firstSource, 1 });
            object secondIndexZero = constructor.Invoke(new object[] { secondSource, 0 });

            Assert.That(
                (bool)equalsMethod.Invoke(firstIndexZero, new[] { firstIndexZero }),
                Is.True);
            Assert.That(
                (bool)equalsMethod.Invoke(firstIndexZero, new[] { firstIndexOne }),
                Is.False);
            Assert.That(
                (bool)equalsMethod.Invoke(firstIndexZero, new[] { secondIndexZero }),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(firstSource);
            Object.DestroyImmediate(secondSource);
        }
    }

    [Test]
    public void PresentationColorIsWrittenAsLinearDotsBaseColor()
    {
        World world = new("EnemyPresentationColorTest");
        GameObject root = new("Enemy");
        EnemyFlockBridge bridge = root.AddComponent<EnemyFlockBridge>();
        EnemyFlockRuntimeService runtime = new(null);
        Entity entity = world.EntityManager.CreateEntity(
            typeof(EnemyFlockAgent),
            typeof(URPMaterialPropertyBaseColor));
        Color sourceColor = new(0.5f, 0.25f, 0.75f, 1f);

        try
        {
            SetField(runtime, "_world", world);
            SetField(runtime, "_entityManager", world.EntityManager);
            SetProperty(bridge, "Runtime", runtime);
            SetProperty(bridge, "Entity", entity);

            MethodInfo method = typeof(EnemyFlockBridge).GetMethod(
                "SetPresentationColor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(bridge, new object[] { sourceColor });

            URPMaterialPropertyBaseColor actual =
                world.EntityManager.GetComponentData<URPMaterialPropertyBaseColor>(entity);
            Color expected = sourceColor.linear;
            Assert.That(actual.Value.x, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.Value.y, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.Value.z, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.Value.w, Is.EqualTo(expected.a).Within(0.0001f));
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

    [Test]
    public void FlockShaderUsesEntitiesGraphicsBaseColorContract()
    {
        const string shaderPath = "Assets/_Project/Scripts/Flocking/ECS/EnemyFlockToon.shader";
        Assert.That(File.Exists(shaderPath), Is.True);

        string shaderSource = File.ReadAllText(shaderPath);
        Assert.That(shaderSource, Does.Contain("UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)"));
        Assert.That(shaderSource, Does.Contain("UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES"));
        Assert.That(shaderSource, Does.Contain("SetupEnemyFlockMaterialPropertyCaches"));

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        Assert.That(shader, Is.Not.Null);
        Material material = new(shader);
        try
        {
            Assert.That(material.HasProperty("_BaseColor"), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(material);
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
