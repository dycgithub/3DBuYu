using System;
using System.Reflection;
using EnemySystem;
using EnemySystem.Components;
using GameSystem;
using NUnit.Framework;
using R3;
using Services;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class EnemyDeathRewardChainTests
{
    [TestCase("Assets/_Project/Prefabs/Enemy/EnemyNormal.prefab")]
    [TestCase("Assets/_Project/Prefabs/Enemy/EnemyFast.prefab")]
    [TestCase("Assets/_Project/Prefabs/Enemy/EnemyTank.prefab")]
    public void EnemyKill_AwardsEnergyAndSessionPointsOnce(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, $"找不到敌人 Prefab: {prefabPath}");

        EnemyDeathRewardComponent prefabReward = prefab.GetComponent<EnemyDeathRewardComponent>();
        Assert.That(prefabReward, Is.Not.Null, $"敌人 Prefab 缺少 EnemyDeathRewardComponent: {prefabPath}");

        GameObject instance = Object.Instantiate(prefab);
        RecordingEnergyService energy = new();
        RecordingGameEventService gameEvents = new();
        AlwaysCombatPhase combatPhase = new();

        try
        {
            instance.SetActive(false);
            Enemy enemy = instance.GetComponent<Enemy>();
            EnemyDeathRewardComponent reward = instance.GetComponent<EnemyDeathRewardComponent>();
            Assert.That(enemy, Is.Not.Null);
            Assert.That(reward, Is.Not.Null);

            SetField(reward, "_energy", energy);
            SetField(reward, "_gameEventService", gameEvents);
            SetField(reward, "_combatPhase", combatPhase);

            instance.SetActive(true);
            enemy.InitializeForSpawn(1f, 1f);
            float expectedEnergy = reward.EnergyReward;
            int expectedPoints = reward.PointsReward;

            enemy.Kill();
            enemy.Kill();

            Assert.That(energy.AddCalls, Is.EqualTo(1));
            Assert.That(energy.TotalAdded, Is.EqualTo(expectedEnergy));
            Assert.That(gameEvents.NotifyCalls, Is.EqualTo(1));
            Assert.That(gameEvents.LastPoints, Is.EqualTo(expectedPoints));
            Assert.That(gameEvents.Session.SessionPoints, Is.EqualTo(expectedPoints));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"找不到字段 {fieldName}。");
        field.SetValue(target, value);
    }

    private sealed class RecordingEnergyService : IEnergyService
    {
        public ReadOnlyReactiveProperty<float> CurrentEnergy => null;
        public float MaximumEnergy => float.MaxValue;
        public bool IsDepleted => false;
        public int AddCalls { get; private set; }
        public float TotalAdded { get; private set; }

        // 此测试替身始终处于未耗尽状态，因此不处理耗尽事件订阅。
        public event Action EnergyDepleted
        {
            add { }
            remove { }
        }

        public void Initialize(float initialEnergy, float maximumEnergy) { }
        public bool TrySpend(float baseAmount, EnergySpendKind kind) => true;
        public float Drain(float baseAmount, EnergySpendKind kind) => 0f;

        public float AddEnergy(float amount)
        {
            AddCalls++;
            TotalAdded += amount;
            return amount;
        }

        public float Tick(float deltaTime, float baseDrainPerSecond) => 0f;
    }

    private sealed class RecordingGameEventService : IGameEventService
    {
        public GameSession Session { get; } = new();
        public int NotifyCalls { get; private set; }
        public int LastPoints { get; private set; }

        public event Action<int> EnemyKilled;

        public int NotifyEnemyKilled(int pointsValue)
        {
            NotifyCalls++;
            LastPoints = pointsValue;
            Session.RecordKill(pointsValue);
            EnemyKilled?.Invoke(pointsValue);
            return pointsValue;
        }
    }

    private sealed class AlwaysCombatPhase : ICombatPhaseService
    {
        public bool CanPerformCombatActions => true;
    }
}
