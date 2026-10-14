using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using Interfaces;
using UnityEngine;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>将中央技能效果转发给当前请求中的所有实验目标和发射器。</summary>
    public sealed class CombatPlaygroundSkillEffects :
        ICentralSkillEffectService,
        ICentralSkillEffectResetService
    {
        private readonly ITransmitterGlobalEffectService _transmitterEffects;
        private readonly IBuffService _buffService;
        private readonly HashSet<int> _permanentSourceInstanceIds = new();

        /// <summary>创建只操作实验发射器和当前目标快照的场景效果边界。</summary>
        public CombatPlaygroundSkillEffects(
            ITransmitterGlobalEffectService transmitterEffects,
            IBuffService buffService = null)
        {
            _transmitterEffects = transmitterEffects;
            _buffService = buffService;
        }

        /// <summary>实验场景没有可解锁的第二个发射器。</summary>
        public bool TryUnlockNextTransmitter() => false;

        /// <summary>向请求中的全部运动目标施加击退冲量。</summary>
        public bool TryPushEnemies(
            IReadOnlyList<IDamageable> targets,
            Vector3 sourcePosition,
            float impulseStrength)
        {
            bool affected = false;
            for (int index = 0; targets != null && index < targets.Count; index++)
            {
                if (targets[index] is not IEnemyMotionEffectTarget motionTarget)
                {
                    continue;
                }

                Vector3 direction = targets[index].Position - sourcePosition;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = Vector3.up;
                }

                motionTarget.AddExternalImpulse(direction.normalized * Mathf.Max(0f, impulseStrength));
                affected = true;
            }

            return affected;
        }

        /// <summary>向请求中的全部敌人施加可清理的永久减速。</summary>
        public bool TrySlowEnemies(
            IReadOnlyList<IDamageable> targets,
            float multiplier,
            int sourceInstanceId)
        {
            bool affected = false;
            for (int index = 0; targets != null && index < targets.Count; index++)
            {
                if (targets[index] is not IEnemyMotionEffectTarget motionTarget)
                {
                    continue;
                }

                if (_buffService != null && targets[index] is IBuffTarget buffTarget)
                {
                    _buffService.SetPermanentMovementSpeedMultiplier(
                        buffTarget,
                        Mathf.Max(0f, multiplier),
                        sourceInstanceId);
                    _permanentSourceInstanceIds.Add(sourceInstanceId);
                }
                else if (motionTarget is IPermanentEnemyMotionEffectTarget permanentTarget)
                {
                    permanentTarget.SetPermanentMovementSpeedMultiplier(
                        Mathf.Max(0f, multiplier),
                        sourceInstanceId);
                }
                else
                {
                    motionTarget.SetMovementSpeedMultiplier(multiplier);
                }

                affected = true;
            }

            return affected;
        }

        /// <summary>设置发射器的限时全局射速倍率。</summary>
        public bool TrySetTransmitterSpeed(
            float multiplier,
            int sourceInstanceId,
            float durationSeconds)
        {
            if (_transmitterEffects == null)
            {
                return false;
            }

            _transmitterEffects.SetAttackSpeedMultiplier(
                Mathf.Max(0f, multiplier),
                sourceInstanceId,
                Mathf.Max(0f, durationSeconds));
            return true;
        }

        /// <summary>将配置 Buff 应用到目标快照中的全部有效敌人。</summary>
        public int ApplyBuffs(
            IReadOnlyList<IDamageable> targets,
            BuffDefinitionSO primary,
            BuffDefinitionSO secondary,
            int sourceInstanceId)
        {
            if (_buffService == null || (primary == null && secondary == null))
            {
                return 0;
            }

            int affectedCount = 0;
            for (int index = 0; targets != null && index < targets.Count; index++)
            {
                IDamageable target = targets[index];
                IBuffTarget buffTarget = ResolveBuffTarget(target);
                if (buffTarget == null || !buffTarget.IsActive || !buffTarget.IsAlive ||
                    !CanApply(primary, secondary, buffTarget))
                {
                    continue;
                }

                if (primary != null)
                {
                    _buffService.ApplyBuff(buffTarget, primary, sourceInstanceId);
                }

                if (secondary != null)
                {
                    _buffService.ApplyBuff(buffTarget, secondary, sourceInstanceId);
                }

                affectedCount++;
            }

            return affectedCount;
        }

        /// <summary>选择当前目标快照中生命值最高的敌人作为集火对象。</summary>
        public bool TryFocusHighestHealth(
            IReadOnlyList<IDamageable> targets,
            float durationSeconds,
            int sourceInstanceId)
        {
            if (_transmitterEffects == null || durationSeconds <= 0f)
            {
                return false;
            }

            Enemy highestHealth = null;
            for (int index = 0; targets != null && index < targets.Count; index++)
            {
                Enemy enemy = ResolveEnemy(targets[index]);
                if (enemy != null && !enemy.IsDead &&
                    (highestHealth == null || enemy.CurrentHealth > highestHealth.CurrentHealth))
                {
                    highestHealth = enemy;
                }
            }

            if (highestHealth == null)
            {
                return false;
            }

            _transmitterEffects.SetFocusedTarget(highestHealth, durationSeconds, sourceInstanceId);
            return true;
        }

        /// <summary>清除本场景提交的永久 Buff 与发射器全局状态。</summary>
        public void ClearPermanentEffects()
        {
            if (_buffService != null)
            {
                foreach (int sourceInstanceId in _permanentSourceInstanceIds)
                {
                    _buffService.ClearPermanentMovementSpeedMultipliersBySourceId(sourceInstanceId);
                }
            }

            if (_transmitterEffects is ITransmitterGlobalEffectResetService resetService)
            {
                resetService.ClearAll();
            }

            _permanentSourceInstanceIds.Clear();
        }

        private static IBuffTarget ResolveBuffTarget(IDamageable target)
        {
            if (target is IBuffTarget directTarget)
            {
                return directTarget;
            }

            return target?.Transform != null
                ? target.Transform.GetComponentInParent<IBuffTarget>()
                : null;
        }

        private static Enemy ResolveEnemy(IDamageable target)
        {
            if (target is Enemy enemy)
            {
                return enemy;
            }

            return target?.Transform != null
                ? target.Transform.GetComponentInParent<Enemy>()
                : null;
        }

        private static bool CanApply(
            BuffDefinitionSO primary,
            BuffDefinitionSO secondary,
            IBuffTarget target)
        {
            bool hasHarmfulBuff = primary != null && primary.Polarity == BuffPolarity.Harmful ||
                secondary != null && secondary.Polarity == BuffPolarity.Harmful;
            return !hasHarmfulBuff || target.CanReceiveHarmfulEffects;
        }
    }
}
