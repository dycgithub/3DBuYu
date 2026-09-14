using System;
using CombatSystem;
using EnemySystem;
using EnemySystem.Components;
using Interfaces;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证敌人生命、护盾、Buff 和死亡奖励边界。
    /// </summary>
    public sealed class EnemyMechanicsBehaviorTests
    {
        private GameObject _enemyObject;
        private EnemyAttributesSO _attributes;
        private BuffDefinitionSO _buff;

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

            if (_buff != null)
            {
                UnityEngine.Object.DestroyImmediate(_buff);
            }
        }

        [Test]
        public void EnemyHeal_ClampsToMaximumHealth()
        {
            Enemy enemy = CreateEnemy(100f);
            ReceiveDamage(enemy, 80f);

            float healed = ((IHealable)enemy).Heal(50f);

            Assert.That(healed, Is.EqualTo(50f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(70f));

            healed = ((IHealable)enemy).Heal(100f);

            Assert.That(healed, Is.EqualTo(30f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void TankShield_RejectsHarmfulBuffUntilShieldIsGone()
        {
            _enemyObject = new GameObject("TankShieldBehavior");
            _enemyObject.SetActive(false);
            Enemy enemy = _enemyObject.AddComponent<Enemy>();
            EnemyShieldComponent shield = _enemyObject.AddComponent<EnemyShieldComponent>();
            _attributes = CreateAttributes(100f);
            _attributes.shieldValue = 20f;
            SetAttributes(enemy, _attributes);
            _enemyObject.SetActive(true);
            enemy.InitializeForSpawn(1f, 1f);

            _buff = ScriptableObject.CreateInstance<BuffDefinitionSO>();
            _buff.ModifierType = BuffModifierType.MovementSpeedMultiplier;
            _buff.ModifierValue = 0.5f;
            _buff.Polarity = BuffPolarity.Harmful;

            enemy.ApplyBuff(_buff, 1);
            Assert.That(
                enemy.BuffController.GetModifierMultiplier(BuffModifierType.MovementSpeedMultiplier),
                Is.EqualTo(1f));

            ReceiveDamage(enemy, 20f);
            enemy.ApplyBuff(_buff, 1);

            Assert.That(shield.IsShieldActive, Is.False);
            Assert.That(
                enemy.BuffController.GetModifierMultiplier(BuffModifierType.MovementSpeedMultiplier),
                Is.EqualTo(0.5f));
        }

        [Test]
        public void ConsumedEnemy_UsesNoRewardDeathCause()
        {
            Enemy enemy = CreateEnemy(20f);
            enemy.KillAsConsumed();

            Assert.That(enemy.IsDead, Is.True);
            Assert.That(enemy.CurrentHealth, Is.Zero);
            Assert.That(enemy.DeathCause, Is.EqualTo(EnemyDeathCause.Consumed));
        }

        private Enemy CreateEnemy(float health)
        {
            _enemyObject = new GameObject("EnemyMechanicsTest");
            Enemy enemy = _enemyObject.AddComponent<Enemy>();
            _attributes = CreateAttributes(health);
            SetAttributes(enemy, _attributes);
            enemy.InitializeForSpawn(1f, 1f);
            return enemy;
        }

        private static EnemyAttributesSO CreateAttributes(float health)
        {
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
            attributes.baseHealth = health;
            attributes.hitEffect = EffectSystem.EffectId.None;
            attributes.deathEffect = EffectSystem.EffectId.None;
            return attributes;
        }

        private static void SetAttributes(Enemy enemy, EnemyAttributesSO attributes)
        {
            SerializedObject serialized = new(enemy);
            SerializedProperty property = serialized.FindProperty("enemyAttributes");
            Assert.That(property, Is.Not.Null);
            property.objectReferenceValue = attributes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ReceiveDamage(Enemy enemy, float amount)
        {
            enemy.ReceiveDamageRequest(new DamageRequest
            {
                DamageAmount = amount,
                DamageType = DamageType.Physical,
                HitPosition = enemy.transform.position,
                HitNormal = Vector3.up
            });
        }
    }
}
