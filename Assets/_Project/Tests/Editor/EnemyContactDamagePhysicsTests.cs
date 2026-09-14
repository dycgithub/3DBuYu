using System.Reflection;
using EnemySystem.Components;
using GameSystem;
using NUnit.Framework;
using Services;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证无 Rigidbody 敌人通过 Trigger 与玩家 Rigidbody 接触时实际扣除能量。
    /// </summary>
    public sealed class EnemyContactDamagePhysicsTests
    {
        private GameObject _enemyObject;
        private GameObject _playerObject;
        private EnergyService _energy;

        [Test]
        public void TriggerContact_DrainsEnergyInPhysicsStep()
        {
            _energy = new EnergyService();
            _energy.Initialize(10f, 10f);
            RecordingCombatPhase combatPhase = new();
            SimulationMode originalSimulationMode = Physics.simulationMode;

            try
            {
                Physics.simulationMode = SimulationMode.Script;
                _enemyObject = CreateEnemyObject();
                EnemyContactDamageComponent contact = _enemyObject.GetComponent<EnemyContactDamageComponent>();
                SetField(contact, "_energyService", _energy);
                SetField(contact, "_combatPhase", combatPhase);

                _playerObject = new GameObject("PhysicsPlayer");
                _playerObject.tag = "Player";
                _playerObject.transform.position = new Vector3(5f, 0f, 0f);
                _playerObject.AddComponent<BoxCollider>();
                Rigidbody playerRigidbody = _playerObject.AddComponent<Rigidbody>();
                playerRigidbody.isKinematic = true;
                playerRigidbody.useGravity = false;

                Physics.Simulate(Time.fixedDeltaTime);

                _playerObject.transform.position = Vector3.zero;
                Physics.Simulate(Time.fixedDeltaTime);

                Assert.That(_enemyObject.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(contact.GetComponent<Collider>().isTrigger, Is.True);
                Assert.That(playerRigidbody, Is.Not.Null);
                Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(9f));
            }
            finally
            {
                Physics.simulationMode = originalSimulationMode;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_enemyObject != null)
            {
                Object.DestroyImmediate(_enemyObject);
            }

            if (_playerObject != null)
            {
                Object.DestroyImmediate(_playerObject);
            }

            _energy?.Dispose();
            _enemyObject = null;
            _playerObject = null;
            _energy = null;
        }

        private static GameObject CreateEnemyObject()
        {
            GameObject enemyObject = new("PhysicsEnemy");
            enemyObject.tag = "Enemy";
            enemyObject.SetActive(false);
            SphereCollider collider = enemyObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            enemyObject.AddComponent<EnemyContactDamageComponent>();
            enemyObject.SetActive(true);
            return enemyObject;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private sealed class RecordingCombatPhase : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }
    }
}
