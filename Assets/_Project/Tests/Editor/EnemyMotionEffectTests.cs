using CombatSystem;
using EnemySystem;
using FlockingSystem.ECS;
using Interfaces;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证敌人速度 Buff、ECS 代理击退和对象池生命周期之间的运动契约。
    /// </summary>
    public sealed class EnemyMotionEffectTests
    {
        private readonly System.Collections.Generic.List<Object> _temporaryObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _temporaryObjects.Count - 1; index >= 0; index--)
            {
                if (_temporaryObjects[index] != null)
                {
                    Object.DestroyImmediate(_temporaryObjects[index]);
                }
            }

            _temporaryObjects.Clear();
        }

        [Test]
        public void MovementSpeedMultiplier_ChangesEcsAgentDisplacement()
        {
            EnemyFlockAgent agent = new()
            {
                Velocity = new float3(4f, 0f, 0f),
                SpeedMultiplier = 0.5f,
                MinSpeed = 0f,
                MaxSpeed = 10f
            };

            float3 displacement = EnemyFlockBoidsMath.CalculateMovementDelta(agent, 1f);

            Assert.That(displacement.x, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void PermanentSlow_FromSameSourceIsAppliedOnlyOnce()
        {
            GameObject enemyObject = Track(new GameObject("PermanentSlowEnemy"));
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            BuffDefinitionSO slow = Track(ScriptableObject.CreateInstance<BuffDefinitionSO>());
            slow.ModifierType = BuffModifierType.MovementSpeedMultiplier;
            slow.ModifierValue = 0.5f;

            enemy.ApplyBuff(slow, 101);
            enemy.ApplyBuff(slow, 101);

            Assert.That(
                enemy.BuffController.GetModifierMultiplier(BuffModifierType.MovementSpeedMultiplier),
                Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void DisabledTarget_ClearsMovementBuffBeforeReuse()
        {
            GameObject target = Track(new GameObject("PooledMotionTarget"));
            BuffController controller = target.AddComponent<BuffController>();
            BuffDefinitionSO slow = Track(ScriptableObject.CreateInstance<BuffDefinitionSO>());
            slow.ModifierType = BuffModifierType.MovementSpeedMultiplier;
            slow.ModifierValue = 0.5f;

            controller.ApplyBuff(slow, 102);
            target.SetActive(false);
            target.SetActive(true);

            Assert.That(
                controller.GetModifierMultiplier(BuffModifierType.MovementSpeedMultiplier),
                Is.EqualTo(1f));
        }

        [Test]
        public void ExternalImpulse_IsIncludedInNextEcsTransformSubmission()
        {
            EnemyFlockAgent agent = new()
            {
                Velocity = float3.zero,
                ExternalImpulse = new float3(3f, 0f, 0f),
                SpeedMultiplier = 1f,
                MinSpeed = 0f,
                MaxSpeed = 0f
            };

            float3 displacement = EnemyFlockBoidsMath.CalculateMovementDelta(agent, 1f);

            Assert.That(displacement.x, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void NonEcsEnemy_AcceptsMotionCommandsWithoutThrowing()
        {
            GameObject enemyObject = Track(new GameObject("FallbackMotionEnemy"));
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            IEnemyMotionEffectTarget motion = enemy;

            Assert.DoesNotThrow(() =>
            {
                motion.SetMovementSpeedMultiplier(0.5f);
                motion.AddExternalImpulse(Vector3.right);
            });
            Assert.That(enemy.transform.position, Is.EqualTo(Vector3.right));
        }

        private T Track<T>(T target) where T : Object
        {
            _temporaryObjects.Add(target);
            return target;
        }
    }
}
