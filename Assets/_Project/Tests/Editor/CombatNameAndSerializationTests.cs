using System;
using CombatSystem;
using EffectSystem;
using NUnit.Framework;
using Play;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 验证战斗系统重命名后，ScriptableObject 类型和已有序列化数据仍然可用。
/// </summary>
public sealed class CombatNameAndSerializationTests
{
    private const string ProjectileProfilePath = "Assets/_Project/SO/Bullet/DefaultBulletProfile.asset";
    private const string ProjectileVisualPath = "Assets/_Project/SO/Bullet/DefaultBulletVisual.asset";
    private const string TransmitterFolder = "Assets/_Project/SO/Transmitter";

    [Test]
    public void RenamedProjectileAssets_PreserveSerializedValues()
    {
        ProjectileProfileSO profile = AssetDatabase.LoadAssetAtPath<ProjectileProfileSO>(ProjectileProfilePath);
        ProjectileVisualDefinitionSO visual = AssetDatabase.LoadAssetAtPath<ProjectileVisualDefinitionSO>(ProjectileVisualPath);

        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.EnergyCost, Is.EqualTo(1f));
        Assert.That(profile.SpeedUnitsPerSecond, Is.EqualTo(15f));
        Assert.That(profile.MaximumTravelDistance, Is.EqualTo(50f));
        Assert.That(profile.CollisionRadius, Is.EqualTo(0.1f));
        Assert.That(profile.VisualDefinition, Is.SameAs(visual));

        Assert.That(visual, Is.Not.Null);
        Assert.That(visual.ProjectilePrefab, Is.Not.Null);
        Assert.That(visual.PrewarmInstanceCount, Is.EqualTo(0));
        Assert.That(visual.MaximumRetainedInstanceCount, Is.EqualTo(256));
    }

    [Test]
    public void RenamedTransmitterReferences_PreserveTheDefaultProjectileProfile()
    {
        ProjectileProfileSO profile = AssetDatabase.LoadAssetAtPath<ProjectileProfileSO>(ProjectileProfilePath);
        string[] guids = AssetDatabase.FindAssets("t:TransmitterSO", new[] { TransmitterFolder });

        Assert.That(guids, Has.Length.EqualTo(8));
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TransmitterSO transmitter = AssetDatabase.LoadAssetAtPath<TransmitterSO>(path);
            Assert.That(transmitter, Is.Not.Null, path);
            Assert.That(transmitter.defaultProjectileProfile, Is.SameAs(profile), path);
        }

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerContainer.prefab");
        TransmitterFireController fireController =
            playerPrefab.GetComponentInChildren<TransmitterFireController>(true);

        Assert.That(fireController, Is.Not.Null);
        SerializedObject controllerSerialized = new SerializedObject(fireController);
        Assert.That(controllerSerialized.FindProperty("projectileProfile"), Is.Null);
        Assert.That(controllerSerialized.FindProperty("attacksPerSecond"), Is.Null);
    }

    /// <summary>
    /// 验证投射物视觉和效果目录保留各自独立的序列化字段名。
    /// </summary>
    [Test]
    public void ProjectileAndEffectAssets_KeepDistinctSerializedFieldNames()
    {
        ProjectileVisualDefinitionSO visual = AssetDatabase.LoadAssetAtPath<ProjectileVisualDefinitionSO>(
            ProjectileVisualPath);
        CombatEffectCatalogSO catalog = AssetDatabase.LoadAssetAtPath<CombatEffectCatalogSO>(
            "Assets/_Project/SO/Effects/CombatEffectCatalog.asset");

        Assert.That(visual, Is.Not.Null);
        Assert.That(catalog, Is.Not.Null);

        SerializedObject visualSerialized = new SerializedObject(visual);
        Assert.That(visualSerialized.FindProperty("ProjectilePrefab"), Is.Not.Null);
        Assert.That(visualSerialized.FindProperty("PrewarmInstanceCount"), Is.Not.Null);
        Assert.That(visualSerialized.FindProperty("MaximumRetainedInstanceCount"), Is.Not.Null);
        Assert.That(visualSerialized.FindProperty("Prefab"), Is.Null);
        Assert.That(visualSerialized.FindProperty("PrewarmCount"), Is.Null);
        Assert.That(visualSerialized.FindProperty("MaximumRetained"), Is.Null);

        SerializedObject catalogSerialized = new SerializedObject(catalog);
        SerializedProperty entries = catalogSerialized.FindProperty("_entries");
        Assert.That(entries, Is.Not.Null);
        Assert.That(entries.arraySize, Is.GreaterThan(0));

        SerializedProperty firstEntry = entries.GetArrayElementAtIndex(0);
        Assert.That(firstEntry.FindPropertyRelative("Prefab"), Is.Not.Null);
        Assert.That(firstEntry.FindPropertyRelative("PrewarmCount"), Is.Not.Null);
        Assert.That(firstEntry.FindPropertyRelative("MaximumRetained"), Is.Not.Null);
    }

    [Test]
    public void CombatConfigurationTypes_UseTheRequiredScriptableObjectSuffix()
    {
        Type[] configurationTypes =
        {
            typeof(ProjectileProfileSO),
            typeof(ProjectileVisualDefinitionSO),
            typeof(ProjectileEffectDefinitionSO),
            typeof(ApplyBuffEffectDefinitionSO),
            typeof(PlayVFXEffectDefinitionSO),
            typeof(TrajectoryDefinitionSO),
            typeof(ProjectileTrajectoryDefinitionSO),
            typeof(HitscanTrajectoryDefinitionSO),
            typeof(TransmitterAttackModifierDefinitionSO),
            typeof(AddDamageAttackModifierDefinitionSO),
            typeof(MultiplyDamageAttackModifierDefinitionSO),
            typeof(AddProjectileCountAttackModifierDefinitionSO),
            typeof(AddPenetrationAttackModifierDefinitionSO),
            typeof(ItemCombatDefinitionSO),
            typeof(BuffDefinitionSO),
            typeof(SkillDefinitionSO)
        };

        foreach (Type configurationType in configurationTypes)
            Assert.That(configurationType.Name, Does.EndWith("SO"), configurationType.FullName);
    }
}
