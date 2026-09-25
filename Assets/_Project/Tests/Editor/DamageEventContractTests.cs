using CombatSystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;

public sealed class DamageEventContractTests
{
    private GameObject _targetObject;

    [SetUp]
    public void SetUp()
    {
        _targetObject = new GameObject("DamageEventTarget");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_targetObject);
    }

    [Test]
    public void AppliedDamage_PublishesCompleteEvent()
    {
        FakeReceiver receiver = new(_targetObject.transform);
        var processor = new DamageRequestProcessor();
        DamageEvent receivedEvent = default;
        int eventCount = 0;
        processor.DamageApplied += damageEvent =>
        {
            receivedEvent = damageEvent;
            eventCount++;
        };

        DamageRequest request = new()
        {
            AttackInstanceId = 7,
            SourceInstanceId = 11,
            DamageAmount = 25f,
            DamageType = DamageType.Fire,
            HitPosition = new Vector3(1f, 2f, 3f),
            HitNormal = Vector3.up,
            WasCritical = true
        };

        bool applied = processor.TryApplyDamage(receiver, request, out DamageResult result);

        Assert.IsTrue(applied);
        Assert.AreEqual(1, eventCount);
        Assert.AreEqual(request.AttackInstanceId, receivedEvent.AttackInstanceId);
        Assert.AreEqual(request.SourceInstanceId, receivedEvent.SourceInstanceId);
        Assert.AreEqual(request.HitPosition, receivedEvent.HitPosition);
        Assert.AreEqual(request.HitNormal, receivedEvent.HitNormal);
        Assert.AreEqual(request.DamageType, receivedEvent.DamageType);
        Assert.AreEqual(request.WasCritical, receivedEvent.WasCritical);
        Assert.AreEqual(result.AppliedDamage, receivedEvent.AppliedDamage);
        Assert.AreEqual(result.RemainingHealth, receivedEvent.RemainingHealth);
        Assert.AreSame(receiver, receivedEvent.Target);
    }

    [Test]
    public void BlockedDamage_DoesNotPublishEvent()
    {
        var receiver = new FakeReceiver(_targetObject.transform)
        {
            Result = new DamageResult
            {
                Outcome = DamageOutcome.Blocked,
                RemainingHealth = 100f
            }
        };
        var processor = new DamageRequestProcessor();
        int eventCount = 0;
        processor.DamageApplied += _ => eventCount++;

        bool applied = processor.TryApplyDamage(
            receiver,
            new DamageRequest { DamageAmount = 10f },
            out DamageResult result);

        Assert.IsFalse(applied);
        Assert.AreEqual(DamageOutcome.Blocked, result.Outcome);
        Assert.AreEqual(0, eventCount);
    }

    private sealed class FakeReceiver : IDamageable, IDamageRequestReceiver
    {
        private readonly Transform _transform;

        public FakeReceiver(Transform transform)
        {
            _transform = transform;
        }

        public DamageResult Result { get; set; } = new()
        {
            Outcome = DamageOutcome.Applied,
            AppliedDamage = 10f,
            RemainingHealth = 90f
        };

        public Vector3 Position => _transform.position;
        public bool IsAlive => true;
        public Transform Transform => _transform;

        public DamageResult ReceiveDamageRequest(in DamageRequest request)
        {
            return Result;
        }
    }
}
