using EnemySystem;
using EnemySystem.Components;
using FlockingSystem.ECS;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 验证敌人实体配置与 ECS Flocking 配置的职责边界。
/// </summary>
public sealed class EnemyConfigurationContractTests
{
    private const string EnemyFolder = "Assets/_Project/SO/Enemy";
    private const string FlockSettingsPath = "Assets/_Project/SO/Flocking/EnemyFlockSettings.asset";

    [Test]
    public void EnemyAttributes_ContainNonMovementParameters()
    {
        EnemyAttributesSO normal = LoadEnemy("EnemyAttributes_Normal.asset");
        EnemyAttributesSO fast = LoadEnemy("EnemyAttributes_Fast.asset");
        EnemyAttributesSO tank = LoadEnemy("EnemyAttributes_Tank.asset");

        Assert.That(normal.baseHealth, Is.EqualTo(10f));
        Assert.That(normal.enemyType, Is.EqualTo(EnemyType.Normal));
        Assert.That(fast.enemyType, Is.EqualTo(EnemyType.Fast));
        Assert.That(fast.dodgeChance, Is.EqualTo(0.3f));
        Assert.That(fast.dodgeDistance, Is.EqualTo(2f));
        Assert.That(tank.enemyType, Is.EqualTo(EnemyType.Tank));
        Assert.That(tank.shieldValue, Is.EqualTo(50f));
        Assert.That(tank.shieldRegenRate, Is.EqualTo(5f));
        Assert.That(tank.shieldRegenDelay, Is.EqualTo(3f));
        Assert.That(tank.explosionRadius, Is.EqualTo(5f));
    }

    [Test]
    public void EnemyAttributes_DoNotStoreFlockingMovementParameters()
    {
        EnemyAttributesSO normal = LoadEnemy("EnemyAttributes_Normal.asset");
        SerializedObject serialized = new SerializedObject(normal);

        Assert.That(serialized.FindProperty("baseSpeed"), Is.Null);
        Assert.That(serialized.FindProperty("flockNeighbourDistance"), Is.Null);
        Assert.That(serialized.FindProperty("flockSeparationDistance"), Is.Null);
        Assert.That(serialized.FindProperty("flockRotationSpeed"), Is.Null);

        EnemyFlockSettingsSO settings = AssetDatabase.LoadAssetAtPath<EnemyFlockSettingsSO>(FlockSettingsPath);
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.GetProfileForEnemyType(EnemyType.Normal, 1f).MaxSpeed, Is.EqualTo(5f));
        Assert.That(settings.GetProfileForEnemyType(EnemyType.Fast, 1f).MaxSpeed, Is.EqualTo(10f));
        Assert.That(settings.GetProfileForEnemyType(EnemyType.Tank, 1f).MaxSpeed, Is.EqualTo(4f));
    }

    [Test]
    public void EnemyPrefabs_DoNotStoreDuplicateBehaviorParameters()
    {
        AssertPrefabHasNoLocalParameters("EnemyNormal.prefab");
        AssertPrefabHasNoLocalParameters("EnemyFast.prefab");
        AssertPrefabHasNoLocalParameters("EnemyTank.prefab");
    }

    [Test]
    public void EnemyPrefabs_ContainDeathRewardComponent()
    {
        AssertPrefabHasDeathReward("EnemyNormal.prefab");
        AssertPrefabHasDeathReward("EnemyFast.prefab");
        AssertPrefabHasDeathReward("EnemyTank.prefab");
    }

    private static EnemyAttributesSO LoadEnemy(string fileName)
    {
        EnemyAttributesSO attributes = AssetDatabase.LoadAssetAtPath<EnemyAttributesSO>(
            $"{EnemyFolder}/{fileName}");
        Assert.That(attributes, Is.Not.Null, fileName);
        return attributes;
    }

    private static void AssertPrefabHasNoLocalParameters(string fileName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/_Project/Prefabs/Enemy/{fileName}");
        Assert.That(prefab, Is.Not.Null, fileName);

        Enemy enemy = prefab.GetComponent<Enemy>();
        Assert.That(enemy, Is.Not.Null, fileName);

        SerializedObject enemySerialized = new SerializedObject(enemy);
        Assert.That(enemySerialized.FindProperty("hitEffect"), Is.Null, fileName);
        Assert.That(enemySerialized.FindProperty("deathEffect"), Is.Null, fileName);

        EnemyDodgeComponent dodge = prefab.GetComponent<EnemyDodgeComponent>();
        if (dodge != null)
        {
            SerializedObject serialized = new SerializedObject(dodge);
            Assert.That(serialized.FindProperty("dodgeChance"), Is.Null, fileName);
            Assert.That(serialized.FindProperty("dodgeDistance"), Is.Null, fileName);
        }

        EnemyShieldComponent shield = prefab.GetComponent<EnemyShieldComponent>();
        if (shield != null)
        {
            SerializedObject serialized = new SerializedObject(shield);
            Assert.That(serialized.FindProperty("shieldValue"), Is.Null, fileName);
            Assert.That(serialized.FindProperty("shieldRegenRate"), Is.Null, fileName);
            Assert.That(serialized.FindProperty("shieldRegenDelay"), Is.Null, fileName);
        }

        EnemyDeathExplosionComponent explosion = prefab.GetComponent<EnemyDeathExplosionComponent>();
        if (explosion != null)
        {
            SerializedObject serialized = new SerializedObject(explosion);
            Assert.That(serialized.FindProperty("timePenalty"), Is.Null, fileName);
            Assert.That(serialized.FindProperty("explosionRadius"), Is.Null, fileName);
            Assert.That(serialized.FindProperty("overlapBufferCapacity"), Is.Null, fileName);
        }

        EnemyDeathRewardComponent reward = prefab.GetComponent<EnemyDeathRewardComponent>();
        if (reward != null)
        {
            SerializedObject serialized = new SerializedObject(reward);
            Assert.That(serialized.FindProperty("energyReward"), Is.Null, fileName);
            Assert.That(serialized.FindProperty("pointsReward"), Is.Null, fileName);
        }
    }

    private static void AssertPrefabHasDeathReward(string fileName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/_Project/Prefabs/Enemy/{fileName}");
        Assert.That(prefab, Is.Not.Null, fileName);
        Assert.That(prefab.GetComponent<EnemyDeathRewardComponent>(), Is.Not.Null, fileName);
    }
}
