using GameSystem;
using NUnit.Framework;

namespace _Project.Tests.Editor
{
    /// <summary>合法动作耗尽能量时，终局必须等动作完成后才可发生。</summary>
    public sealed class EnergyActionTests
    {
        [Test]
        public void DepletionWaitsForOutermostAction()
        {
            using EnergyService energy = new();
            energy.Initialize(10f, 10f);
            int notifications = 0;
            energy.EnergyDepleted += () => notifications++;
            using (energy.BeginAction())
            {
                using (energy.BeginAction())
                {
                    Assert.That(energy.TrySpend(10f, Services.EnergySpendKind.Skill), Is.True);
                }
                Assert.That(notifications, Is.Zero);
            }
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void RefundedFailedActionDoesNotEndRun()
        {
            using EnergyService energy = new();
            energy.Initialize(10f, 10f);
            int notifications = 0;
            energy.EnergyDepleted += () => notifications++;
            using (energy.BeginAction())
            {
                energy.TrySpend(10f, Services.EnergySpendKind.Shot);
                energy.AddEnergy(10f);
            }
            Assert.That(notifications, Is.Zero);
        }
    }
}
