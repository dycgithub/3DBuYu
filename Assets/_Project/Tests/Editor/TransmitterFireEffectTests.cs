using System;
using CombatSystem;
using EffectSystem;
using NUnit.Framework;
using Services;
using UnityEngine;

public sealed class TransmitterFireEffectTests
{
    [Test]
    public void AttackSucceededPlaysConfiguredEffectAtOrigin()
    {
        FakeAttackService attackService = new();
        RecordingEffectService effectService = new();
        TransmitterFireEffect effect = new(attackService, effectService);
        Vector3 origin = new(1f, 2f, 3f);

        try
        {
            effect.Start();
            attackService.Raise(CreateInput(EffectId.TransmitterFire, origin));

            Assert.That(effectService.PlayCount, Is.EqualTo(1));
            Assert.That(effectService.LastEffectId, Is.EqualTo(EffectId.TransmitterFire));
            Assert.That(effectService.LastPosition, Is.EqualTo(origin));
        }
        finally
        {
            effect.Dispose();
        }
    }

    [Test]
    public void AttackSucceededAttachesEffectToOriginTransform()
    {
        FakeAttackService attackService = new();
        RecordingEffectService effectService = new();
        TransmitterFireEffect effect = new(attackService, effectService);
        GameObject firePoint = new("FirePoint");

        try
        {
            effect.Start();
            attackService.Raise(CreateInput(
                EffectId.TransmitterFire,
                Vector3.one,
                firePoint.transform));

            Assert.That(effectService.LastParent, Is.SameAs(firePoint.transform));
        }
        finally
        {
            effect.Dispose();
            UnityEngine.Object.DestroyImmediate(firePoint);
        }
    }

    [Test]
    public void NoneEffectDoesNotPlay()
    {
        FakeAttackService attackService = new();
        RecordingEffectService effectService = new();
        TransmitterFireEffect effect = new(attackService, effectService);

        try
        {
            effect.Start();
            attackService.Raise(CreateInput(EffectId.None, Vector3.zero));

            Assert.That(effectService.PlayCount, Is.EqualTo(0));
        }
        finally
        {
            effect.Dispose();
        }
    }

    [Test]
    public void DisposeStopsListeningToAttackEvents()
    {
        FakeAttackService attackService = new();
        RecordingEffectService effectService = new();
        TransmitterFireEffect effect = new(attackService, effectService);

        effect.Start();
        effect.Dispose();
        attackService.Raise(CreateInput(EffectId.TransmitterFire, Vector3.one));

        Assert.That(effectService.PlayCount, Is.EqualTo(0));
    }

    private static TransmitterAttackInput CreateInput(
        EffectId effectId,
        Vector3 origin,
        Transform originTransform = null)
    {
        return new TransmitterAttackInput(
            1,
            0,
            null,
            origin,
            Vector3.forward,
            1f,
            10f,
            1f,
            1,
            0,
            0f,
            1f,
            "test-port",
            null,
            effectId,
            originTransform);
    }

    private sealed class FakeAttackService : ITransmitterAttackService
    {
        public event Action<TransmitterAttackInput> AttackSucceeded;

        public bool TryExecuteAttack(in TransmitterAttackInput input)
        {
            return false;
        }

        public void Raise(TransmitterAttackInput input)
        {
            AttackSucceeded?.Invoke(input);
        }
    }

    private sealed class RecordingEffectService : IEffectService
    {
        public int PlayCount { get; private set; }
        public EffectId LastEffectId { get; private set; }
        public Vector3 LastPosition { get; private set; }
        public Transform LastParent { get; private set; }

        public void Play(EffectId effectId, Vector3 position)
        {
            PlayCount++;
            LastEffectId = effectId;
            LastPosition = position;
        }

        public void Play(EffectId effectId, Vector3 position, Transform parent)
        {
            Play(effectId, position);
            LastParent = parent;
        }

        public void Stop(EffectId effectId)
        {
        }
    }
}
