using Interfaces;
using NUnit.Framework;
using CombatSystem;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>不依赖场景的攻击契约测试，验证数据和纯运行时规则。</summary>
    public sealed class CombatAttackContractTests
    {
        private BulletProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<BulletProfile>();
            _profile.Speed = 20f;
            _profile.MaxDistance = 100f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_profile != null)
                Object.DestroyImmediate(_profile);
        }

        [Test]
        public void ProjectileRuntime_CopiesLaunchInfoAndPenetration()
        {
            var info = new ProjectileInfo
            {
                ProjectileId = 12,
                AttackId = 7,
                SourceId = 3,
                Profile = _profile,
                Origin = new Vector3(1f, 2f, 3f),
                Direction = Vector3.forward,
                Damage = 25f,
                Speed = 20f,
                MaxDistance = 100f,
                Radius = 0.2f,
                Penetration = 2,
                DamageType = DamageType.Physical
            };

            var runtime = new ProjectileRuntime(info, null);

            Assert.That(runtime.Info.AttackId, Is.EqualTo(7));
            Assert.That(runtime.Position, Is.EqualTo(info.Origin));
            Assert.That(runtime.RemainingPenetration, Is.EqualTo(2));
            Assert.That(runtime.RemainingLife, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void DamagePipeline_UsesStructuredReceiver()
        {
            var target = new FakeDamageTarget();
            var pipeline = new DamagePipeline();
            var request = new DamageRequest
            {
                AttackId = 11,
                SourceId = 4,
                BaseDamage = 30f,
                DamageType = DamageType.Fire,
                HitPoint = Vector3.one,
                HitNormal = Vector3.up,
                IsCritical = true
            };

            bool applied = pipeline.TryApply(target, request, out DamageResult result);

            Assert.That(applied, Is.True);
            Assert.That(target.ReceivedRequest.AttackId, Is.EqualTo(11));
            Assert.That(target.ReceivedRequest.DamageType, Is.EqualTo(DamageType.Fire));
            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Applied));
            Assert.That(result.ActualDamage, Is.EqualTo(30f));
        }

        [Test]
        public void DamagePipeline_RejectsDeadTargetBeforeCallingReceiver()
        {
            var target = new FakeDamageTarget { Alive = false };
            var pipeline = new DamagePipeline();
            var request = new DamageRequest { BaseDamage = 10f };

            bool applied = pipeline.TryApply(target, request, out DamageResult result);

            Assert.That(applied, Is.False);
            Assert.That(target.ReceiveCallCount, Is.Zero);
            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Invalid));
        }

        [Test]
        public void AttackModifierPipeline_AppliesConfiguredModifiers()
        {
            var damageModifier = ScriptableObject.CreateInstance<MultiplyDamageAttackModifier>();
            damageModifier.multiplier = 2f;
            var countModifier = ScriptableObject.CreateInstance<AddProjectileCountAttackModifierSO>();
            countModifier.amount = 2;

            var info = new AttackInfo
            {
                Damage = 10f,
                ProjectileCount = 1,
                Penetration = 0
            };

            new AttackModifierPipeline().Apply(
                ref info,
                new IAttackModifier[] { damageModifier, countModifier });

            Assert.That(info.Damage, Is.EqualTo(20f));
            Assert.That(info.ProjectileCount, Is.EqualTo(3));

            Object.DestroyImmediate(damageModifier);
            Object.DestroyImmediate(countModifier);
        }

        [Test]
        public void BuffController_CombinesStacksAndRemovesBySource()
        {
            var target = new GameObject("BuffTarget");
            var controller = target.AddComponent<BuffController>();
            var config = ScriptableObject.CreateInstance<BuffConfig>();
            config.Type = BuffType.DamageTakenMultiplier;
            config.Value = 2f;
            config.Duration = 10f;
            config.StackPolicy = BuffStackPolicy.AddStack;
            config.MaxStacks = 2;

            controller.AddBuff(config, 99);
            controller.AddBuff(config, 99);

            Assert.That(controller.GetModifier(BuffType.DamageTakenMultiplier), Is.EqualTo(4f));

            controller.RemoveBySource(99);

            Assert.That(controller.GetModifier(BuffType.DamageTakenMultiplier), Is.EqualTo(1f));

            Object.DestroyImmediate(config);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void AttackCooldownRegistry_IsolatesDifferentTurrets()
        {
            var registry = new AttackCooldownRegistry();

            registry.MarkUsed(10, 0);

            Assert.That(registry.IsReady(10, 0, 1f), Is.False);
            Assert.That(registry.IsReady(11, 0, 1f), Is.True);
        }

        [Test]
        public void ProjectileRuntimePool_ReusesLogicalRuntime()
        {
            var pool = new ProjectileRuntimePool();
            var info = new ProjectileInfo
            {
                Profile = _profile,
                Origin = Vector3.zero,
                Direction = Vector3.forward,
                Speed = 10f,
                MaxDistance = 10f,
                Penetration = 1
            };

            ProjectileRuntime first = pool.Rent(info, null);
            first.TraveledDistance = 4f;
            pool.Return(first);
            ProjectileRuntime second = pool.Rent(info, null);

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.HitTargetIds, Is.Empty);
            Assert.That(second.TraveledDistance, Is.Zero);
            Assert.That(second.RemainingPenetration, Is.EqualTo(1));

            pool.Dispose();
        }

        [Test]
        public void AttackExecutor_SubmitsProjectileCountAsOneBatch()
        {
            var spawner = new FakeProjectileSpawner();
            var executor = new AttackExecutor(
                spawner,
                null,
                null,
                new AttackCooldownRegistry());
            var attack = new AttackInfo
            {
                Profile = _profile,
                Damage = 10f,
                ProjectileCount = 3,
                Direction = Vector3.forward
            };

            Assert.That(executor.TryExecute(ref attack, 1f), Is.True);
            Assert.That(spawner.BatchCount, Is.EqualTo(3));
            Assert.That(spawner.SingleCallCount, Is.Zero);
        }

        [Test]
        public void AttackBuilder_BuildsFromNeutralPortContext()
        {
            _profile.Damage = 10f;
            var context = new PortAttackContext(
                sourceId: 21,
                portIndex: 2,
                profile: _profile,
                origin: new Vector3(1f, 2f, 3f),
                direction: Vector3.forward,
                fireRate: 2f,
                damageMultiplier: 2f,
                rangeMultiplier: 0.5f,
                projectileCount: 2,
                penetration: 1,
                criticalChance: 0f,
                criticalDamage: 1f);
            var builder = new AttackBuilder(null, new AttackModifierPipeline());

            bool built = builder.TryBuild(in context, out AttackInfo attack);

            Assert.That(built, Is.True);
            Assert.That(attack.SourceId, Is.EqualTo(21));
            Assert.That(attack.PortIndex, Is.EqualTo(2));
            Assert.That(attack.Origin, Is.EqualTo(context.Origin));
            Assert.That(attack.Damage, Is.EqualTo(20f));
            Assert.That(attack.MaxDistance, Is.EqualTo(50f));
            Assert.That(attack.ProjectileCount, Is.EqualTo(2));
            Assert.That(attack.Penetration, Is.EqualTo(1));
        }

        [Test]
        public void ProjectileInfoFactory_CopiesAttackSnapshot()
        {
            var attack = new AttackInfo
            {
                AttackId = 9,
                SourceId = 8,
                Profile = _profile,
                Origin = Vector3.one,
                Direction = Vector3.right,
                Damage = 17f,
                Speed = 23f,
                MaxDistance = 40f,
                Radius = 0.3f,
                Penetration = 2,
                DamageType = DamageType.Fire,
                IsCritical = true
            };

            ProjectileInfo projectile = ProjectileInfoFactory.FromAttack(in attack);

            Assert.That(projectile.AttackId, Is.EqualTo(9));
            Assert.That(projectile.SourceId, Is.EqualTo(8));
            Assert.That(projectile.Damage, Is.EqualTo(17f));
            Assert.That(projectile.Penetration, Is.EqualTo(2));
            Assert.That(projectile.DamageType, Is.EqualTo(DamageType.Fire));
            Assert.That(projectile.IsCritical, Is.True);
        }

        [Test]
        public void AttackCooldownRegistry_UsesInjectedClock()
        {
            var clock = new FakeAttackClock { CurrentTime = 10f };
            var registry = new AttackCooldownRegistry(clock);

            registry.MarkUsed(1, 0);
            clock.CurrentTime = 10.5f;

            Assert.That(registry.IsReady(1, 0, 1f), Is.False);
            clock.CurrentTime = 11f;
            Assert.That(registry.IsReady(1, 0, 1f), Is.True);
        }

        private sealed class FakeDamageTarget : IDamageable, IDamageReceiver
        {
            public bool Alive { get; set; } = true;
            public int ReceiveCallCount { get; private set; }
            public DamageRequest ReceivedRequest { get; private set; }

            public Vector3 Position => Vector3.zero;
            public bool IsAlive => Alive;
            public Transform Transform => null;

            public void TakeDamage(float amount)
            {
                Alive = false;
            }

            public DamageResult ReceiveDamage(in DamageRequest request)
            {
                ReceiveCallCount++;
                ReceivedRequest = request;
                return new DamageResult
                {
                    Outcome = DamageOutcome.Applied,
                    ActualDamage = request.BaseDamage,
                    RemainingHealth = 70f
                };
            }
        }

        private sealed class FakeProjectileSpawner : IProjectileSpawner
        {
            public int SingleCallCount { get; private set; }
            public int BatchCount { get; private set; }

            public bool TrySpawn(in ProjectileInfo info)
            {
                SingleCallCount++;
                return true;
            }

            public bool TrySpawnBatch(in ProjectileInfo info, int count)
            {
                BatchCount = count;
                return count > 0;
            }
        }

        private sealed class FakeAttackClock : IAttackClock
        {
            public float CurrentTime { get; set; }

            public float Time => CurrentTime;
        }
    }
}
