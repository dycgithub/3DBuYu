using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using UnityEngine;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>
    /// 将现有中央技能动作映射到单发射器实验场景，不创建或查找 CentralCore。
    /// </summary>
    public sealed class CombatPlaygroundSkillEffects :
        ICentralSkillEffectService,
        ICentralSkillEffectResetService
    {
        private readonly EnemySystem.Enemy _target;
        private readonly ITransmitterGlobalEffectService _transmitterEffects;
        private readonly IBuffService _buffService;
        private readonly HashSet<int> _permanentSourceInstanceIds = new();

        /// <summary>创建只操作当前敌人和当前发射器的技能效果边界。</summary>
        public CombatPlaygroundSkillEffects(
            EnemySystem.Enemy target,
            ITransmitterGlobalEffectService transmitterEffects,
            IBuffService buffService = null)
        {
            _target = target;
            _transmitterEffects = transmitterEffects;
            _buffService = buffService;
        }

        /// <summary>单发射器实验场景没有可解锁的下一个发射器。</summary>
        public bool TryUnlockNextTransmitter() => false;

        /// <summary>向当前敌人施加击退冲量。</summary>
        public bool TryPushEnemies(
            IReadOnlyList<IDamageable> targets,
            Vector3 sourcePosition,
            float impulseStrength)
        {
            if (!ContainsTarget(targets) || !(_target is IEnemyMotionEffectTarget motionTarget))
            {
                return false;
            }

            Vector3 direction = _target.transform.position - sourcePosition;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.up;
            }

            motionTarget.AddExternalImpulse(direction.normalized * Mathf.Max(0f, impulseStrength));
            return true;
        }

        /// <summary>向当前敌人施加来源可追踪的永久减速。</summary>
        public bool TrySlowEnemies(
            IReadOnlyList<IDamageable> targets,
            float multiplier,
            int sourceInstanceId)
        {
            if (!ContainsTarget(targets) || !(_target is IEnemyMotionEffectTarget motionTarget))
            {
                return false;
            }

            if (_buffService != null && _target is IBuffTarget buffTarget)
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

            return true;
        }

        /// <summary>设置当前发射器的限时全局射速倍率。</summary>
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

        /// <summary>向实验目标应用提供的 Buff。</summary>
        public int ApplyBuffs(
            IReadOnlyList<IDamageable> targets,
            BuffDefinitionSO primary,
            BuffDefinitionSO secondary,
            int sourceInstanceId)
        {
            if (_buffService == null || !ContainsTarget(targets) ||
                !(_target is IBuffTarget buffTarget) ||
                (primary == null && secondary == null))
            {
                return 0;
            }

            if (primary != null)
            {
                _buffService.ApplyBuff(buffTarget, primary, sourceInstanceId);
            }

            if (secondary != null)
            {
                _buffService.ApplyBuff(buffTarget, secondary, sourceInstanceId);
            }

            return 1;
        }

        /// <summary>将实验发射器锁定到当前目标。</summary>
        public bool TryFocusHighestHealth(
            IReadOnlyList<IDamageable> targets,
            float durationSeconds,
            int sourceInstanceId)
        {
            if (_transmitterEffects == null || !ContainsTarget(targets) ||
                _target.IsDead || durationSeconds <= 0f)
            {
                return false;
            }

            _transmitterEffects.SetFocusedTarget(
                _target,
                durationSeconds,
                sourceInstanceId);
            return true;
        }

        /// <summary>清除本地效果服务持有的发射器倍率。</summary>
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

        private bool ContainsTarget(IReadOnlyList<IDamageable> targets)
        {
            if (_target == null || targets == null)
            {
                return false;
            }

            for (int index = 0; index < targets.Count; index++)
            {
                if (ReferenceEquals(targets[index], _target))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
