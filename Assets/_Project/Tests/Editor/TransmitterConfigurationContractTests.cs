using CombatSystem;
using NUnit.Framework;
using Play;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 验证发射器基础数值、子弹机制配置和攻击快照之间的职责边界。
/// </summary>
public sealed class TransmitterConfigurationContractTests
{
    private const string CentralPath = "Assets/_Project/SO/CentralBase.asset";
    private const string ProfilePath = "Assets/_Project/SO/Bullet/DefaultBulletProfile.asset";
    private const string TransmitterFolder = "Assets/_Project/SO/Transmitter";
    private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PlayerContainer.prefab";

    [Test]
    public void EveryTransmitter_ContainsBaseAttackValues()
    {
        string[] guids = AssetDatabase.FindAssets("t:TransmitterSO", new[] { TransmitterFolder });

        Assert.That(guids, Has.Length.EqualTo(8));
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TransmitterSO transmitter = AssetDatabase.LoadAssetAtPath<TransmitterSO>(path);

            Assert.That(transmitter, Is.Not.Null, path);
            Assert.That(transmitter.baseDamage, Is.EqualTo(10f), path);
            Assert.That(transmitter.baseRange, Is.EqualTo(50f), path);
            Assert.That(transmitter.attacksPerSecond, Is.EqualTo(2f), path);
            Assert.That(transmitter.projectileCount, Is.EqualTo(1), path);
            Assert.That(transmitter.criticalChance, Is.EqualTo(0.05f), path);
            Assert.That(transmitter.criticalDamage, Is.EqualTo(1.5f), path);
            Assert.That(transmitter.penetration, Is.EqualTo(0), path);
            Assert.That(transmitter.defaultProjectileProfile, Is.Not.Null, path);
        }
    }

    [Test]
    public void CentralAndFireController_DoNotStoreTransmitterAttackValues()
    {
        CentralSO central = AssetDatabase.LoadAssetAtPath<CentralSO>(CentralPath);
        Assert.That(central, Is.Not.Null);

        SerializedObject centralSerialized = new SerializedObject(central);
        Assert.That(centralSerialized.FindProperty("baseRange"), Is.Null);
        Assert.That(centralSerialized.FindProperty("baseFireRate"), Is.Null);
        Assert.That(centralSerialized.FindProperty("baseProjectileCount"), Is.Null);
        Assert.That(centralSerialized.FindProperty("baseRotationSpeed"), Is.Null);

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        TransmitterFireController controller = playerPrefab.GetComponentInChildren<TransmitterFireController>(true);
        Assert.That(controller, Is.Not.Null);

        SerializedObject controllerSerialized = new SerializedObject(controller);
        Assert.That(controllerSerialized.FindProperty("projectileProfile"), Is.Null);
        Assert.That(controllerSerialized.FindProperty("attacksPerSecond"), Is.Null);
    }

    [Test]
    public void ProjectileProfile_StoresMechanicsButNotTransmitterBaseDamage()
    {
        ProjectileProfileSO profile = AssetDatabase.LoadAssetAtPath<ProjectileProfileSO>(ProfilePath);
        Assert.That(profile, Is.Not.Null);

        SerializedObject serialized = new SerializedObject(profile);
        Assert.That(serialized.FindProperty("BaseDamage"), Is.Null);
        Assert.That(profile.EnergyCost, Is.EqualTo(1f));
        Assert.That(profile.SpeedUnitsPerSecond, Is.EqualTo(15f));
        Assert.That(profile.MaximumTravelDistance, Is.EqualTo(50f));
        Assert.That(profile.CollisionRadius, Is.EqualTo(0.1f));
    }

    [Test]
    public void AttackDataBuilder_UsesTransmitterBaseDamageAndRange()
    {
        ProjectileProfileSO profile = ProjectileProfileSO.CreateRuntimeProfile("test-profile");
        var input = new TransmitterAttackInput(
            sourceInstanceId: 1,
            transmitterPortIndex: 0,
            projectileProfile: profile,
            originPosition: Vector3.zero,
            launchDirection: Vector3.forward,
            baseDamage: 10f,
            baseRange: 40f,
            attacksPerSecond: 2f,
            requestedProjectileCount: 1,
            requestedPenetrationCount: 0,
            criticalHitChance: 0f,
            criticalHitDamageMultiplier: 1f);
        var buildService = new TransmitterAttackBuildService(null);
        TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);
        var builder = new AttackDataBuilder();

        try
        {
            Assert.That(builder.TryBuildAttackData(in input, in build, out AttackData attack), Is.True);
            Assert.That(attack.DamageAmount, Is.EqualTo(10f));
            Assert.That(attack.MaximumTravelDistance, Is.EqualTo(40f));
            Assert.That(attack.EnergyCost, Is.EqualTo(1f));
            Assert.That(attack.SpeedUnitsPerSecond, Is.EqualTo(15f));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void MissingProjectileProfile_DoesNotCreateRuntimeFallback()
    {
        var input = new TransmitterAttackInput(
            sourceInstanceId: 1,
            transmitterPortIndex: 0,
            projectileProfile: null,
            originPosition: Vector3.zero,
            launchDirection: Vector3.forward,
            baseDamage: 10f,
            baseRange: 50f,
            attacksPerSecond: 2f,
            requestedProjectileCount: 1,
            requestedPenetrationCount: 0,
            criticalHitChance: 0f,
            criticalHitDamageMultiplier: 1f);
        var buildService = new TransmitterAttackBuildService(null);
        TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);
        var builder = new AttackDataBuilder();

        Assert.That(build.ProjectileProfile, Is.Null);
        Assert.That(builder.TryBuildAttackData(in input, in build, out _), Is.False);
    }
}
