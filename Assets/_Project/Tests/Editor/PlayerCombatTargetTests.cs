using System.Reflection;
using CombatSystem;
using GameSystem;
using Interfaces;
using NUnit.Framework;
using Play;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证玩家以能量作为敌人伤害、治疗和减速目标的契约。
    /// </summary>
    public sealed class PlayerCombatTargetTests
    {
        private GameObject _playerObject;
        private EnergyService _energy;

        [TearDown]
        public void TearDown()
        {
            _energy?.Dispose();
            if (_playerObject != null)
            {
                Object.DestroyImmediate(_playerObject);
            }
        }

        [Test]
        public void PlayerCombatTarget_DamageAndHealUseEnergy()
        {
            _playerObject = new GameObject("PlayerCombatTargetTest");
            _playerObject.tag = "Player";
            _playerObject.AddComponent<BuffController>();
            PlayerCombatTarget target = _playerObject.AddComponent<PlayerCombatTarget>();
            _energy = new EnergyService();
            _energy.Initialize(100f, 100f);
            SetField(target, "_energy", _energy);

            DamageResult result = target.ReceiveDamageRequest(new DamageRequest
            {
                DamageAmount = 30f,
                DamageType = DamageType.Physical
            });

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Applied));
            Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(70f));
            Assert.That(((IHealable)target).Heal(50f), Is.EqualTo(30f));
            Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(100f));
        }

        [Test]
        public void EnemyProjectileLaunch_DefaultsToEnemyTarget()
        {
            ProjectileProfileSO profile = ScriptableObject.CreateInstance<ProjectileProfileSO>();
            try
            {
                ProjectileLaunchRequest request = new(
                    1,
                    profile,
                    Vector3.zero,
                    Vector3.forward,
                    1f);

                Assert.That(request.TargetMode, Is.EqualTo(ProjectileTargetMode.Enemy));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field?.SetValue(target, value);
        }
    }
}
