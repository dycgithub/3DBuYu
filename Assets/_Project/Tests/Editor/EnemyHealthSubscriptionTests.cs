using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using EnemySystem.Components;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyHealthSubscriptionTests
{
    /// <summary>验证生命初始化和实际受伤会按顺序发布归一化值。</summary>
    [Test]
    public void Health01_PublishesInitialAndDamagedValues()
    {
        GameObject root = new("Enemy");
        root.SetActive(false);
        EnemyHealthComponent health = root.AddComponent<EnemyHealthComponent>();
        Enemy enemy = root.GetComponent<Enemy>();
        EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
        List<float> values = new();

        try
        {
            attributes.baseHealth = 100f;
            SetField(enemy, "enemyAttributes", attributes);
            root.SetActive(true);
            health.InitializeForSpawn(1f);

            R3.Observable<float> healthStream = health.Health01;
            using (R3.ObservableSubscribeExtensions.Subscribe(
                healthStream,
                value => values.Add(value)))
            {
                health.ReceiveDamageRequest(new DamageRequest
                {
                    DamageAmount = 25f,
                    DamageType = DamageType.Physical
                });
            }

            Assert.That(values, Is.EqualTo(new[] { 1f, 0.75f }));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(attributes);
        }
    }

    /// <summary>验证对象池归还和重新生成会恢复血量视觉输入。</summary>
    [Test]
    public void Health01_PublishesResetAndReinitializedValues()
    {
        GameObject root = new("Enemy");
        root.SetActive(false);
        EnemyHealthComponent health = root.AddComponent<EnemyHealthComponent>();
        Enemy enemy = root.GetComponent<Enemy>();
        EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
        List<float> values = new();

        try
        {
            attributes.baseHealth = 100f;
            SetField(enemy, "enemyAttributes", attributes);
            root.SetActive(true);
            health.InitializeForSpawn(1f);

            using (R3.ObservableSubscribeExtensions.Subscribe(
                health.Health01,
                value => values.Add(value)))
            {
                health.ResetForReuse();
                health.InitializeForSpawn(2f);
            }

            Assert.That(values, Is.EqualTo(new[] { 1f, 0f, 1f }));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(attributes);
        }
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType()
            .GetField(
                name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(target, value);
    }
}
