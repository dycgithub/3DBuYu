using System;
using System.Collections.Generic;
using System.Reflection;
using CombatSystem;
using EnemySystem;
using EnemySystem.Components;
using GameSystem;
using NUnit.Framework;
using R3;
using Services;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证敌人 Trigger 接触伤害的物理配置、能量数据流和对象池生命周期。
    /// </summary>
    public sealed class EnemyContactDamageContractTests
    {
        private const string EnemyPrefabFolder = "Assets/_Project/Prefabs/Enemy";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PlayerContainer.prefab";

        private readonly List<GameObject> _objects = new();
        private RecordingEnergyService _energy;
        private RecordingCombatPhase _combatPhase;

        [SetUp]
        public void SetUp()
        {
            _energy = new RecordingEnergyService();
            _combatPhase = new RecordingCombatPhase();
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < _objects.Count; index++)
            {
                if (_objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
            _energy.Dispose();
        }

        [Test]
        public void PlayerContainer_HasPlayerRigidbodyAndTag()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            if (prefab == null)
            {
                return;
            }

            Assert.That(prefab.CompareTag("Player"), Is.True);
            Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null);
        }

        [Test]
        public void EnemyPrefabs_UseTriggerWithoutRigidbodyAndMountContactDamage()
        {
            string[] prefabNames = { "EnemyNormal.prefab", "EnemyFast.prefab", "EnemyTank.prefab" };

            for (int index = 0; index < prefabNames.Length; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{EnemyPrefabFolder}/{prefabNames[index]}");

                Assert.That(prefab, Is.Not.Null, prefabNames[index]);
                if (prefab == null)
                {
                    continue;
                }

                Collider collider = prefab.GetComponent<Collider>();
                Assert.That(prefab.GetComponent<Rigidbody>(), Is.Null, prefabNames[index]);
                Assert.That(collider, Is.Not.Null, prefabNames[index]);
                if (collider != null)
                {
                    Assert.That(collider.isTrigger, Is.True, prefabNames[index]);
                }

                Assert.That(
                    prefab.GetComponent<EnemyContactDamageComponent>(),
                    Is.Not.Null,
                    prefabNames[index]);
            }
        }

        [Test]
        public void EnemyContactDamage_IsOptionalByMounting()
        {
            RequireComponent[] requirements = (RequireComponent[])typeof(Enemy).GetCustomAttributes(
                typeof(RequireComponent), true);

            for (int index = 0; index < requirements.Length; index++)
            {
                Assert.That(
                    requirements[index].m_Type0,
                    Is.Not.EqualTo(typeof(EnemyContactDamageComponent)));
                Assert.That(
                    requirements[index].m_Type1,
                    Is.Not.EqualTo(typeof(EnemyContactDamageComponent)));
                Assert.That(
                    requirements[index].m_Type2,
                    Is.Not.EqualTo(typeof(EnemyContactDamageComponent)));
            }
        }

        [Test]
        public void PlayerTriggerContact_DrainsEnergyWithEnemyContactSource()
        {
            EnemyContactDamageComponent contact = CreateEnemyContact();
            Collider playerCollider = CreateCollider("Player", "Player");

            SimulateTrigger(contact, playerCollider);

            Assert.That(_energy.DrainCalls, Has.Count.EqualTo(1));
            Assert.That(_energy.DrainCalls[0].Amount, Is.EqualTo(1f));
            Assert.That(_energy.DrainCalls[0].Kind, Is.EqualTo(EnergySpendKind.EnemyContact));
        }

        [Test]
        public void PlayerTriggerContact_RespectsConfiguredCooldown()
        {
            EnemyContactDamageComponent contact = CreateEnemyContact();
            Collider playerCollider = CreateCollider("Player", "Player");

            SimulateTrigger(contact, playerCollider);
            SimulateTrigger(contact, playerCollider);

            Assert.That(_energy.DrainCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public void EnemyTriggerContact_DoesNotDrainEnergy()
        {
            EnemyContactDamageComponent contact = CreateEnemyContact();
            Collider enemyCollider = CreateCollider("OtherEnemy", "Enemy");

            SimulateTrigger(contact, enemyCollider);

            Assert.That(_energy.DrainCalls, Is.Empty);
        }

        [Test]
        public void DeadEnemy_DoesNotDealContactDamage()
        {
            EnemyContactDamageComponent contact = CreateEnemyContact();
            Collider playerCollider = CreateCollider("Player", "Player");
            contact.GetComponent<Enemy>().Kill();

            SimulateTrigger(contact, playerCollider);

            Assert.That(_energy.DrainCalls, Is.Empty);
        }

        [Test]
        public void InactiveCombatPhase_DoesNotDealContactDamage()
        {
            EnemyContactDamageComponent contact = CreateEnemyContact();
            Collider playerCollider = CreateCollider("Player", "Player");
            _combatPhase.CanPerformCombatActions = false;

            SimulateTrigger(contact, playerCollider);

            Assert.That(_energy.DrainCalls, Is.Empty);
        }

        [Test]
        public void DisableAndPoolReuse_ClearContactCooldowns()
        {
            EnemyContactDamageComponent contact = CreateEnemyContact();
            Collider playerCollider = CreateCollider("Player", "Player");

            SimulateTrigger(contact, playerCollider);
            contact.gameObject.SetActive(false);
            contact.gameObject.SetActive(true);
            SimulateTrigger(contact, playerCollider);
            contact.OnReturnToPool();
            contact.OnRentFromPool();
            SimulateTrigger(contact, playerCollider);

            Assert.That(_energy.DrainCalls, Has.Count.EqualTo(3));
        }

        private EnemyContactDamageComponent CreateEnemyContact()
        {
            GameObject enemyObject = Track(new GameObject("Enemy"));
            enemyObject.tag = "Enemy";
            enemyObject.SetActive(false);
            enemyObject.AddComponent<SphereCollider>().isTrigger = true;
            EnemyContactDamageComponent contact = enemyObject.AddComponent<EnemyContactDamageComponent>();
            Inject(contact, "_energyService", _energy);
            Inject(contact, "_combatPhase", _combatPhase);
            enemyObject.SetActive(true);
            return contact;
        }

        private Collider CreateCollider(string name, string tag)
        {
            GameObject target = Track(new GameObject(name));
            target.tag = tag;
            return target.AddComponent<BoxCollider>();
        }

        private static void SimulateTrigger(
            EnemyContactDamageComponent contact,
            Collider other)
        {
            MethodInfo triggerMethod = typeof(EnemyContactDamageComponent).GetMethod(
                "OnTriggerEnter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(triggerMethod, Is.Not.Null);
            if (triggerMethod != null)
            {
                triggerMethod.Invoke(contact, new object[] { other });
            }
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private static void Inject(object target, string fieldName, object value)
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
            public bool CanPerformCombatActions { get; set; } = true;
        }

        private sealed class RecordingEnergyService : IEnergyService, IDisposable
        {
            private readonly EnergyService _inner = new();

            public ReadOnlyReactiveProperty<float> CurrentEnergy => _inner.CurrentEnergy;
            public float MaximumEnergy => _inner.MaximumEnergy;
            public bool IsDepleted => _inner.IsDepleted;
            public List<DrainCall> DrainCalls { get; } = new();

            public event Action EnergyDepleted
            {
                add { _inner.EnergyDepleted += value; }
                remove { _inner.EnergyDepleted -= value; }
            }

            public void Initialize(float initialEnergy, float maximumEnergy)
            {
                _inner.Initialize(initialEnergy, maximumEnergy);
            }

            public bool TrySpend(float baseAmount, EnergySpendKind kind)
            {
                return _inner.TrySpend(baseAmount, kind);
            }

            public float Drain(float baseAmount, EnergySpendKind kind)
            {
                DrainCalls.Add(new DrainCall(baseAmount, kind));
                return _inner.Drain(baseAmount, kind);
            }

            public float AddEnergy(float amount)
            {
                return _inner.AddEnergy(amount);
            }

            public float Tick(float deltaTime, float baseDrainPerSecond)
            {
                return _inner.Tick(deltaTime, baseDrainPerSecond);
            }

            public void Dispose()
            {
                _inner.Dispose();
            }
        }

        private readonly struct DrainCall
        {
            public float Amount { get; }
            public EnergySpendKind Kind { get; }

            public DrainCall(float amount, EnergySpendKind kind)
            {
                Amount = amount;
                Kind = kind;
            }
        }
    }
}
