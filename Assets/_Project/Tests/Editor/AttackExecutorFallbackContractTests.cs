using CombatSystem;
using NUnit.Framework;
using Services;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证攻击执行器在通用发射器拒绝请求时仍可使用旧投射物边界。
    /// </summary>
    public sealed class AttackExecutorFallbackContractTests
    {
        [Test]
        public void GenericLauncherFailure_UsesLegacySpawner()
        {
            var spawner = new RecordingSpawner();
            var launcher = new RejectingLauncher();
            var executor = new AttackExecutor(
                spawner,
                energy: null,
                phase: new CombatPhase(),
                cooldowns: new AttackCooldownRegistry(),
                projectileLauncher: launcher);
            AttackData attack = new()
            {
                SourceInstanceId = 1,
                TransmitterPortIndex = 0,
                DamageAmount = 1f,
                ProjectileCount = 1
            };

            bool executed = executor.TryExecuteAttack(ref attack, 1f);

            Assert.That(executed, Is.True);
            Assert.That(launcher.CallCount, Is.EqualTo(1));
            Assert.That(spawner.BatchCallCount, Is.EqualTo(1));
        }

        private sealed class CombatPhase : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }

        private sealed class RejectingLauncher : IProjectileLauncher
        {
            public int CallCount { get; private set; }

            public bool Launch(in ProjectileLaunchRequest request)
            {
                CallCount++;
                return false;
            }

            public bool Launch(in AttackData attackData)
            {
                CallCount++;
                return false;
            }
        }

        private sealed class RecordingSpawner : IProjectileSpawner
        {
            public int BatchCallCount { get; private set; }

            public bool TrySpawnProjectile(in ProjectileData projectileData) => true;

            public bool TrySpawnProjectileBatch(
                in ProjectileData projectileData,
                int projectileCount)
            {
                BatchCallCount++;
                return true;
            }
        }
    }
}
