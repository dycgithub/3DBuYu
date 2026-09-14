using System;
using System.Collections.Generic;
using CombatSystem;
using EffectSystem;
using EnemySystem;
using EnemySystem.Components;
using Interfaces;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Utils;
using Object = UnityEngine.Object;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证伤害类型、防御行为、Buff 生命周期和命中来源上下文契约。
    /// </summary>
    public sealed class DamageBuffContractTests
    {
        private readonly List<Object> _runtimeObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _runtimeObjects.Count - 1; index >= 0; index--)
            {
                if (_runtimeObjects[index] != null)
                {
                    Object.DestroyImmediate(_runtimeObjects[index]);
                }
            }

            _runtimeObjects.Clear();
        }

        [Test]
        public void PureDamage_BypassesInterceptors()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            int interceptorCallCount = 0;
            enemy.RegisterPreDamageInterceptor(
                (Enemy target, float originalDamage, ref float finalDamage) =>
                {
                    interceptorCallCount++;
                    return false;
                });

            DamageResult result = ReceiveDamage(enemy, 10f, DamageType.Pure);

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Applied));
            Assert.That(result.AppliedDamage, Is.EqualTo(10f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(90f));
            Assert.That(interceptorCallCount, Is.Zero);
        }

        [Test]
        public void PureDamage_BypassesShield()
        {
            Enemy enemy = CreateEnemy<Enemy>(attributes => attributes.shieldValue = 10f);
            enemy.gameObject.AddComponent<EnemyShieldComponent>();

            DamageResult pureResult = ReceiveDamage(enemy, 5f, DamageType.Pure);
            DamageResult normalResult = ReceiveDamage(enemy, 5f, DamageType.Physical);

            Assert.That(pureResult.AppliedDamage, Is.EqualTo(5f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(95f));
            Assert.That(normalResult.Outcome, Is.EqualTo(DamageOutcome.Blocked));
            Assert.That(normalResult.AppliedDamage, Is.Zero);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(95f));
        }

        [Test]
        public void PureDamage_BypassesDamageTakenMultiplier()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            BuffDefinitionSO damageTaken = CreateBuff(
                modifierValue: 0.5f,
                modifierType: BuffModifierType.DamageTakenMultiplier);
            enemy.ApplyBuff(damageTaken, 17);

            DamageResult pureResult = ReceiveDamage(enemy, 10f, DamageType.Pure);
            DamageResult normalResult = ReceiveDamage(enemy, 10f, DamageType.Physical);

            Assert.That(pureResult.AppliedDamage, Is.EqualTo(10f));
            Assert.That(normalResult.AppliedDamage, Is.EqualTo(5f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(85f));
        }

        [Test]
        public void NormalDamage_StillUsesShieldAndDamageTakenMultiplier()
        {
            Enemy enemy = CreateEnemy<Enemy>(attributes => attributes.shieldValue = 10f);
            enemy.gameObject.AddComponent<EnemyShieldComponent>();
            BuffDefinitionSO damageTaken = CreateBuff(
                modifierValue: 0.5f,
                modifierType: BuffModifierType.DamageTakenMultiplier);
            enemy.ApplyBuff(damageTaken, 17);

            DamageResult result = ReceiveDamage(enemy, 20f, DamageType.Physical);

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Applied));
            Assert.That(result.AppliedDamage, Is.EqualTo(5f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(95f));
        }

        [Test]
        public void NormalDamage_UsesInterceptorResult()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            enemy.RegisterPreDamageInterceptor(
                (Enemy target, float originalDamage, ref float finalDamage) =>
                {
                    finalDamage = 3f;
                    return true;
                });

            DamageResult result = ReceiveDamage(enemy, 10f, DamageType.Physical);

            Assert.That(result.AppliedDamage, Is.EqualTo(3f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(97f));
        }

        [Test]
        public void ZeroDamage_IsBlockedWithoutDefensiveSideEffects()
        {
            DamageTrackingEnemy enemy = CreateEnemy<DamageTrackingEnemy>();
            int interceptorCallCount = 0;
            enemy.RegisterPreDamageInterceptor(
                (Enemy target, float originalDamage, ref float finalDamage) =>
                {
                    interceptorCallCount++;
                    return false;
                });

            DamageResult result = ReceiveDamage(enemy, 0f, DamageType.Physical);

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Blocked));
            Assert.That(result.AppliedDamage, Is.Zero);
            Assert.That(result.WasApplied, Is.False);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(100f));
            Assert.That(enemy.DamageCallbackCount, Is.Zero);
            Assert.That(interceptorCallCount, Is.Zero);
        }

        [Test]
        public void AddStack_ClampsAtMaximumAndMultipliesEachLayer()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            BuffDefinitionSO definition = CreateBuff(
                modifierValue: 0.5f,
                stackPolicy: BuffStackPolicy.AddStack,
                maximumStackCount: 3);

            enemy.ApplyBuff(definition, 20);
            enemy.ApplyBuff(definition, 20);
            enemy.ApplyBuff(definition, 20);
            enemy.ApplyBuff(definition, 20);

            Assert.That(
                enemy.BuffController.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                Is.EqualTo(0.125f).Within(0.0001f));
        }

        [Test]
        public void RefreshDuration_PreservesTheExistingBuffDefinition()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            BuffDefinitionSO original = CreateBuff(
                modifierValue: 0.5f,
                durationSeconds: 2f,
                stackPolicy: BuffStackPolicy.RefreshDuration);
            BuffDefinitionSO refreshed = CreateBuff(
                modifierValue: 0.25f,
                durationSeconds: 5f,
                stackPolicy: BuffStackPolicy.RefreshDuration);

            enemy.ApplyBuff(original, 21);
            enemy.ApplyBuff(refreshed, 21);

            Assert.That(
                enemy.BuffController.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                Is.EqualTo(0.5f));
        }

        [Test]
        public void Replace_UsesTheNewDefinitionAndResetsTheLayerCount()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            BuffDefinitionSO stacked = CreateBuff(
                modifierValue: 0.5f,
                stackPolicy: BuffStackPolicy.AddStack,
                maximumStackCount: 3);
            BuffDefinitionSO replacement = CreateBuff(
                modifierValue: 0.8f,
                stackPolicy: BuffStackPolicy.Replace);

            enemy.ApplyBuff(stacked, 22);
            enemy.ApplyBuff(stacked, 22);
            enemy.ApplyBuff(replacement, 22);

            Assert.That(
                enemy.BuffController.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                Is.EqualTo(0.8f));
        }

        [Test]
        public void Independent_CreatesSeparateInstancesForTheSameSource()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            BuffDefinitionSO definition = CreateBuff(
                modifierValue: 0.5f,
                stackPolicy: BuffStackPolicy.Independent);

            enemy.ApplyBuff(definition, 23);
            enemy.ApplyBuff(definition, 23);

            Assert.That(
                enemy.BuffController.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                Is.EqualTo(0.25f));
        }

        [Test]
        public void DifferentSources_RemainSeparateAndCanBeRemovedIndividually()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            BuffDefinitionSO definition = CreateBuff(modifierValue: 0.5f);

            enemy.ApplyBuff(definition, 24);
            enemy.ApplyBuff(definition, 25);
            enemy.BuffController.RemoveBySourceId(24);

            Assert.That(
                enemy.BuffController.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                Is.EqualTo(0.5f));
        }

        [Test]
        public void ActiveBuffRuntime_ExpiresAfterItsDuration()
        {
            BuffDefinitionSO definition = CreateBuff(
                modifierValue: 0.5f,
                durationSeconds: 3f);
            ActiveBuffRuntime runtime = new()
            {
                Definition = definition,
                RemainingDurationSeconds = 3f,
                SourceInstanceId = 26
            };

            runtime.AdvanceTime(2f);

            Assert.That(runtime.IsExpired, Is.False);
            Assert.That(runtime.RemainingDurationSeconds, Is.EqualTo(1f));

            runtime.AdvanceTime(1f);

            Assert.That(runtime.IsExpired, Is.True);
        }

        [Test]
        public void BuffController_DisablingOwnerClearsActiveBuffs()
        {
            GameObject owner = new("BuffDisableCleanupTest");
            _runtimeObjects.Add(owner);
            BuffController controller = owner.AddComponent<BuffController>();
            BuffDefinitionSO definition = CreateBuff(modifierValue: 0.5f);
            controller.ApplyBuff(definition, 27);

            owner.SetActive(false);
            owner.SetActive(true);

            Assert.That(
                controller.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                Is.EqualTo(1f));
        }

        [Test]
        public void EnemyResetForReuse_ClearsBuffsBeforeNextRent()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            BuffDefinitionSO definition = CreateBuff(modifierValue: 0.5f);
            enemy.ApplyBuff(definition, 28);

            enemy.ResetForReuse();

            Assert.That(
                enemy.BuffController.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                Is.EqualTo(1f));
             enemy.InitializeForSpawn(1f, 1f);
            DamageResult result = ReceiveDamage(enemy, 10f, DamageType.Physical);
            Assert.That(result.AppliedDamage, Is.EqualTo(10f));
        }

        [Test]
        public void ObjectPoolReuse_DoesNotRetainDisabledBuffState()
        {
            GameObject prefab = new("BuffPoolPrefab");
            _runtimeObjects.Add(prefab);
            prefab.SetActive(false);
            prefab.AddComponent<BuffController>();
            GameObjectPoolService pool = new();

            try
            {
                GameObject instance = pool.Rent(
                    prefab,
                    new PoolSettings(initialCapacity: 0, maximumRetained: 1));
                Assert.That(instance, Is.Not.Null);
                BuffController controller = instance.GetComponent<BuffController>();
                BuffDefinitionSO definition = CreateBuff(modifierValue: 0.5f);
                controller.ApplyBuff(definition, 29);

                Assert.That(pool.Return(instance), Is.True);
                instance = pool.Rent(
                    prefab,
                    new PoolSettings(initialCapacity: 0, maximumRetained: 1));

                Assert.That(
                    instance.GetComponent<BuffController>().GetModifierMultiplier(
                        BuffModifierType.DamageTakenMultiplier),
                    Is.EqualTo(1f));
            }
            finally
            {
                pool.Dispose();
            }
        }

        [Test]
        public void DisabledDodgeComponent_DoesNotRemainRegistered()
        {
            Enemy enemy = CreateEnemy<Enemy>(attributes => attributes.dodgeChance = 1f);
            EnemyDodgeComponent dodge = enemy.gameObject.AddComponent<EnemyDodgeComponent>();
            dodge.enabled = false;

            DamageResult result = ReceiveDamage(enemy, 10f, DamageType.Physical);

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Applied));
            Assert.That(result.AppliedDamage, Is.EqualTo(10f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(90f));
        }

        [Test]
        public void BeforeHitBuff_PreservesSourceInstanceId()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            BuffDefinitionSO definition = CreateBuff(modifierValue: 0.5f);
            ProjectileProfileSO profile = CreateBeforeHitProfile(definition);
            ProjectileData projectile = CreateProjectileData(profile, 731);
            ProjectileSimulationService simulation = CreateSimulation(
                enemy,
                new DamageRequestProcessor());

            try
            {
                Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
                simulation.SimulateStep(1f);

                Assert.That(
                    enemy.BuffController.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                    Is.EqualTo(0.5f));
                enemy.BuffController.RemoveBySourceId(731);
                Assert.That(
                    enemy.BuffController.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                    Is.EqualTo(1f));
            }
            finally
            {
                simulation.Dispose();
            }
        }

        [Test]
        public void FailedDamagePreparation_DoesNotLeaveBeforeHitBuff()
        {
            Enemy enemy = CreateEnemy<Enemy>();
            BuffDefinitionSO definition = CreateBuff(modifierValue: 0.5f);
            ProjectileProfileSO profile = CreateBeforeHitProfile(definition);
            ProjectileData projectile = CreateProjectileData(profile, 732);
            ProjectileSimulationService simulation = CreateSimulation(
                enemy,
                new RejectingDamageProcessor());

            try
            {
                Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
                simulation.SimulateStep(1f);

                Assert.That(
                    enemy.BuffController.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                    Is.EqualTo(1f));
            }
            finally
            {
                simulation.Dispose();
            }
        }

        [Test]
        public void InvalidDamageTarget_IsRejectedBeforeReceiverSideEffects()
        {
            DamageRequestProcessor processor = new();
            InvalidDamageTarget target = new();
            DamageRequest request = new()
            {
                DamageAmount = 10f,
                DamageType = DamageType.Physical
            };

            bool accepted = processor.TryApplyDamage(target, in request, out DamageResult result);

            Assert.That(accepted, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Invalid));
            Assert.That(target.ReceiveCallCount, Is.Zero);
        }

        private T CreateEnemy<T>(Action<EnemyAttributesSO> configure = null)
            where T : Enemy
        {
            GameObject instance = new(typeof(T).Name + "DamageTest");
            _runtimeObjects.Add(instance);
            instance.AddComponent<BoxCollider>();
            T enemy = instance.AddComponent<T>();

            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
            attributes.baseHealth = 100f;
            attributes.hitEffect = EffectId.None;
            attributes.deathEffect = EffectId.None;
            configure?.Invoke(attributes);
            _runtimeObjects.Add(attributes);

            SerializedObject serialized = new(enemy);
            SerializedProperty attributesProperty = serialized.FindProperty("enemyAttributes");
            Assert.That(attributesProperty, Is.Not.Null);
            attributesProperty.objectReferenceValue = attributes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
             enemy.InitializeForSpawn(1f, 1f);
            return enemy;
        }

        private BuffDefinitionSO CreateBuff(
            float modifierValue,
            BuffModifierType modifierType = BuffModifierType.DamageTakenMultiplier,
            float durationSeconds = 0f,
            BuffStackPolicy stackPolicy = BuffStackPolicy.RefreshDuration,
            int maximumStackCount = 1)
        {
            BuffDefinitionSO definition = ScriptableObject.CreateInstance<BuffDefinitionSO>();
            definition.ModifierType = modifierType;
            definition.ModifierValue = modifierValue;
            definition.DurationSeconds = durationSeconds;
            definition.StackPolicy = stackPolicy;
            definition.MaximumStackCount = maximumStackCount;
            _runtimeObjects.Add(definition);
            return definition;
        }

        private ProjectileProfileSO CreateBeforeHitProfile(BuffDefinitionSO definition)
        {
            ApplyBuffEffectDefinitionSO effect = ScriptableObject.CreateInstance<ApplyBuffEffectDefinitionSO>();
            effect.BuffDefinition = definition;
            _runtimeObjects.Add(effect);

            ProjectileProfileSO profile = ProjectileProfileSO.CreateRuntimeProfile("BuffProjectile");
            profile.SpeedUnitsPerSecond = 10f;
            profile.MaximumTravelDistance = 10f;
            profile.HitEffectId = EffectId.None;
            profile.ExpiredEffectId = EffectId.None;
            profile.BeforeHitEffectDefinitions = new List<ProjectileEffectDefinitionSO> { effect };
            _runtimeObjects.Add(profile);
            return profile;
        }

        private static ProjectileData CreateProjectileData(
            ProjectileProfileSO profile,
            int sourceInstanceId)
        {
            return new ProjectileData
            {
                AttackInstanceId = 1,
                SourceInstanceId = sourceInstanceId,
                ProjectileProfile = profile,
                OriginPosition = Vector3.zero,
                LaunchDirection = Vector3.forward,
                DamageAmount = 1f,
                SpeedUnitsPerSecond = 10f,
                MaximumTravelDistance = 10f,
                CollisionRadius = 0.1f,
                DamageType = DamageType.Physical
            };
        }

        private static ProjectileSimulationService CreateSimulation(
            Enemy enemy,
            IDamageRequestProcessor damageProcessor)
        {
            return new ProjectileSimulationService(
                new ProjectileViewPool(null),
                new ProjectileRuntimePool(),
                new AlwaysHitCollisionQuery(enemy),
                damageProcessor,
                new ProjectileEffectDispatcher(null));
        }

        private static DamageResult ReceiveDamage(
            Enemy enemy,
            float damageAmount,
            DamageType damageType)
        {
            DamageRequest request = new()
            {
                DamageAmount = damageAmount,
                DamageType = damageType,
                HitPosition = Vector3.zero,
                HitNormal = Vector3.up
            };
            return enemy.ReceiveDamageRequest(in request);
        }

        private sealed class DamageTrackingEnemy : Enemy
        {
            public int DamageCallbackCount { get; private set; }

            protected override void OnDamageTaken(float damageAmount)
            {
                DamageCallbackCount++;
            }
        }

        private sealed class AlwaysHitCollisionQuery : IProjectileCollisionQuery
        {
            private readonly IDamageable _target;

            public AlwaysHitCollisionQuery(IDamageable target)
            {
                _target = target;
            }

            public bool TryFindSweepCollision(
                ProjectileRuntime projectile,
                float travelDistance,
                out RaycastHit hit,
                out IDamageable target)
            {
                hit = default;
                target = _target;
                return true;
            }

            public bool TryFindRaycastCollision(
                ProjectileData projectileData,
                out RaycastHit hit,
                out IDamageable target)
            {
                hit = default;
                target = _target;
                return true;
            }
        }

        private sealed class RejectingDamageProcessor : IDamageRequestProcessor
        {
            public bool TryApplyDamage(
                IDamageable target,
                in DamageRequest request,
                out DamageResult result)
            {
                result = new DamageResult
                {
                    Outcome = DamageOutcome.Blocked
                };
                return false;
            }
        }

        private sealed class InvalidDamageTarget : IDamageable, IDamageRequestReceiver
        {
            public int ReceiveCallCount { get; private set; }
            public Vector3 Position => Vector3.zero;
            public bool IsAlive => false;
            public Transform Transform => null;

            public DamageResult ReceiveDamageRequest(in DamageRequest request)
            {
                ReceiveCallCount++;
                return new DamageResult { Outcome = DamageOutcome.Applied };
            }
        }
    }
}
