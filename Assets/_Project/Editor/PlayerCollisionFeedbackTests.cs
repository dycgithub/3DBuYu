using System.Reflection;
using EffectSystem;
using NUnit.Framework;
using Play;
using Services;
using UnityEngine;

/// <summary>Locks player hit feedback to enemy hierarchy tags and real contact positions.</summary>
public sealed class PlayerCollisionFeedbackTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject _player;
    private GameObject _enemy;

    /// <summary>Destroys temporary colliders after each feedback regression test.</summary>
    [TearDown]
    public void TearDown()
    {
        if (_enemy != null)
        {
            Object.DestroyImmediate(_enemy);
        }

        if (_player != null)
        {
            Object.DestroyImmediate(_player);
        }
    }

    /// <summary>An untagged local enemy child collider still produces feedback at its surface.</summary>
    [Test]
    public void Hit_UsesRootTagAndPoint()
    {
        _player = new GameObject("PlayerRoot");
        _player.AddComponent<Rigidbody>();
        PlayerCollisionFeedback feedback = _player.AddComponent<PlayerCollisionFeedback>();

        RecordingEffectService effects = new();
        SetField(feedback, "_combatPhase", new ActiveCombatPhase());
        SetField(feedback, "_effectService", effects);

        _enemy = new GameObject("EnemyRoot");
        _enemy.tag = "Enemy";
        _enemy.transform.position = new Vector3(3f, 0f, 0f);
        GameObject child = new("UntaggedEnemyCollider");
        child.transform.SetParent(_enemy.transform, false);
        BoxCollider collider = child.AddComponent<BoxCollider>();

        typeof(PlayerCollisionFeedback)
            .GetMethod("OnTriggerEnter", PrivateInstance)
            .Invoke(feedback, new object[] { collider });

        Assert.That(effects.PlayCount, Is.EqualTo(1));
        Assert.That(effects.LastEffect, Is.EqualTo(EffectId.PlayerDamage));
        Assert.That(effects.LastPosition, Is.EqualTo(collider.ClosestPoint(_player.transform.position)));
    }

    /// <summary>Sets an injected dependency without constructing a VContainer scope.</summary>
    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Missing field: {name}");
        field.SetValue(target, value);
    }

    private sealed class ActiveCombatPhase : ICombatPhaseService
    {
        public bool CanPerformCombatActions => true;
    }

    private sealed class RecordingEffectService : IEffectService
    {
        public int PlayCount { get; private set; }
        public EffectId LastEffect { get; private set; }
        public Vector3 LastPosition { get; private set; }

        public void Play(EffectId effectId, Vector3 position)
        {
            PlayCount++;
            LastEffect = effectId;
            LastPosition = position;
        }

        public void Play(EffectId effectId, Vector3 position, Transform parent)
        {
            Play(effectId, position);
        }

        public void Stop(EffectId effectId)
        {
        }
    }
}
