using CombatSystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;

public sealed class BuffMannerSchedulingTests
{
    [Test]
    public void HealingAccumulatesAtTenHertz()
    {
        BuffDefinitionSO definition = ScriptableObject.CreateInstance<BuffDefinitionSO>();
        definition.ModifierType = BuffModifierType.HealingOverTime;
        definition.HealingPerSecond = 100f;
        definition.DurationSeconds = 1f;

        try
        {
            BuffManner manner = new();
            TestBuffTarget target = new();
            manner.RegisterTarget(target);

            Assert.That(manner.TryApplyBuffIfAbsent(target, definition, 1), Is.True);

            manner.Process(0.08f);
            Assert.That(target.TotalHealed, Is.EqualTo(0f).Within(0.0001f));

            manner.Process(0.02f);
            Assert.That(target.TotalHealed, Is.EqualTo(10f).Within(0.0001f));

            manner.Process(0.1f);
            Assert.That(target.TotalHealed, Is.EqualTo(20f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    private sealed class TestBuffTarget : IBuffTarget, IHealable
    {
        public float TotalHealed { get; private set; }

        public bool IsActive => true;
        public bool IsAlive => true;
        public bool CanReceiveHarmfulEffects => true;

        public float Heal(float amount)
        {
            TotalHealed += amount;
            return amount;
        }

        public void OnBuffModifiersChanged(BuffModifierType modifierType)
        {
        }
    }
}
