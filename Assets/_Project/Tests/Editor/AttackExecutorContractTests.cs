using Interfaces;
using NUnit.Framework;
using Services;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证攻击执行失败时资源扣除与战斗阶段门禁保持一致。
    /// </summary>
    public sealed class AttackExecutorContractTests
    {
        [Test]
        public void CombatPhaseChangedAfterEnergySpend_RefundsEnergy()
        {
            var energy = new GameSystem.EnergyService();
            energy.Initialize(10f, 10f);
            var phase = new PhaseThatChangesAfterFirstCheck();
            var spawner = new RecordingProjectileSpawner();
            var executor = new AttackExecutor(
                spawner,
                energy,
                phase,
                new AttackCooldownRegistry());
            AttackData attack = new AttackData
            {
                SourceInstanceId = 1,
                TransmitterPortIndex = 0,
                EnergyCost = 2f,
                ProjectileCount = 1,
                DamageAmount = 1f
            };

            try
            {
                Assert.That(executor.TryExecuteAttack(ref attack, 2f), Is.False);
                Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(10f).Within(0.0001f));
                Assert.That(spawner.SpawnCallCount, Is.Zero);
            }
            finally
            {
                energy.Dispose();
            }
        }

        private sealed class PhaseThatChangesAfterFirstCheck : ICombatPhaseService
        {
            private int _checkCount;

            public bool CanPerformCombatActions => ++_checkCount == 1;
        }

        private sealed class RecordingProjectileSpawner : IProjectileSpawner
        {
            public int SpawnCallCount { get; private set; }

            public bool TrySpawnProjectile(in ProjectileData projectileData)
            {
                SpawnCallCount++;
                return true;
            }

            public bool TrySpawnProjectileBatch(
                in ProjectileData projectileData,
                int projectileCount)
            {
                SpawnCallCount++;
                return true;
            }
        }
    }
}
