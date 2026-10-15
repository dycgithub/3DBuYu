using System.Reflection;
using FlockingSystem.ECS;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyFlockVisualPaletteTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string PaletteFolder = "Assets/_Project/Art/Model/EnemyType/Textures/";
    private const string MaterialFolder = "Assets/_Project/Art/Model/EnemyType/Materials/";
    private const string PrefabFolder = "Assets/_Project/Prefabs/Enemy/";

    /// <summary>Every type material must sample its own packed color palette.</summary>
    [TestCase("Normal")]
    [TestCase("Child")]
    [TestCase("Fast")]
    [TestCase("Tank")]
    [TestCase("Reflect")]
    [TestCase("Exploder")]
    [TestCase("Slow")]
    [TestCase("Healer")]
    [TestCase("StrongHealer")]
    [TestCase("Assault")]
    [TestCase("Growth")]
    [TestCase("Shooter")]
    [TestCase("Mother")]
    public void TypeMaterial_UsesItsPalette(string enemyType)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            MaterialFolder + "MAT_EnemyType_" + enemyType + ".mat");
        Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(
            PaletteFolder + "TEX_" + enemyType + "_Color.png");

        Assert.That(material, Is.Not.Null);
        Assert.That(material.HasProperty("_BaseMap"), Is.True);
        Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(palette));
    }

    /// <summary>The ECS render material must retain the source type's palette texture.</summary>
    [Test]
    public void CreateFlockRenderMaterial_CopiesPaletteTexture()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/_Project/Shader/Enemy/EnemyFlockToon.shader");
        Assert.That(shader, Is.Not.Null);

        Material source = new(shader);
        Texture2D palette = new(2, 2);
        EnemyFlockRuntimeService service = new(null);
        Material renderMaterial = null;

        try
        {
            Assert.That(source.HasProperty("_BaseMap"), Is.True);
            source.SetTexture("_BaseMap", palette);

            MethodInfo createMaterial = typeof(EnemyFlockRuntimeService).GetMethod(
                "CreateFlockRenderMaterial",
                PrivateInstance);
            Assert.That(createMaterial, Is.Not.Null);
            renderMaterial = (Material)createMaterial.Invoke(service, new object[] { source, 0 });

            Assert.That(renderMaterial, Is.Not.Null);
            Assert.That(renderMaterial.GetTexture("_BaseMap"), Is.SameAs(palette));
        }
        finally
        {
            if (renderMaterial != null)
            {
                Object.DestroyImmediate(renderMaterial);
            }

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(palette);
            service.Dispose();
        }
    }

    /// <summary>Appearance scale belongs on the first mesh transform read by ECS, not its root.</summary>
    [TestCase("EnemyNormal", 45.15896f)]
    [TestCase("EnemyNormalChild", 38.61f)]
    [TestCase("EnemyFast", 33.27922f)]
    [TestCase("EnemyTank", 44.766f)]
    [TestCase("EnemyReflect", 63.042f)]
    [TestCase("EnemyExploder", 59.8428f)]
    [TestCase("EnemySlow", 59.2488f)]
    [TestCase("EnemyHealer", 63.7632f)]
    [TestCase("EnemyStrongHealer", 71.496f)]
    [TestCase("EnemyAssault", 50.4105f)]
    [TestCase("EnemyGrowth", 51.723f)]
    [TestCase("EnemyShooter", 54.432f)]
    [TestCase("EnemyMother", 56.3745f)]
    public void EnemyPrefab_ScalesItsMeshOnly(string enemyName, float expectedModelScale)
    {
        GameObject prefab = PrefabUtility.LoadPrefabContents(
            PrefabFolder + enemyName + ".prefab");

        try
        {
            MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>(true);

            Assert.That(meshFilter, Is.Not.Null);
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(meshFilter.transform.localScale.x, Is.EqualTo(expectedModelScale).Within(0.02f));
            Assert.That(meshFilter.transform.localScale.y, Is.EqualTo(expectedModelScale).Within(0.02f));
            Assert.That(meshFilter.transform.localScale.z, Is.EqualTo(expectedModelScale).Within(0.02f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }
}
