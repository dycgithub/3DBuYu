using System.Collections.Generic;
using EnemySystem;
using Interfaces;
using Play;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 将中央主动技能转换为核心、敌人运动和发射器全局效果调用。
    /// 该服务是唯一接触场景中央对象的边界，动作 ScriptableObject 不查询场景。
    /// </summary>
    public sealed class CentralSkillEffectService : ICentralSkillEffectService, ICentralSkillEffectResetService
    {
        private const float FocusedAttackDamageMultiplier = 2f;

        private readonly CentralCore _centralCore;
        private readonly ITransmitterGlobalEffectService _transmitterEffects;
        private readonly IBuffService _buffService;
        private readonly HashSet<int> _permanentSourceInstanceIds = new();
        private readonly List<PermanentSlowTarget> _legacyPermanentSlowTargets = new();

        /// <summary>注入当前中央核心、发射器全局效果和场景级 Buff 服务。</summary>
        public CentralSkillEffectService(
            CentralCore centralCore,
            ITransmitterGlobalEffectService transmitterEffects,
            IBuffService buffService = null)
        {
            _centralCore = centralCore;
            _transmitterEffects = transmitterEffects;
            _buffService = buffService;
        }

        /// <summary>调用中央核心现有端口解锁入口。</summary>
        public bool TryUnlockNextTransmitter()
        {
            return _centralCore != null && _centralCore.TryExpandPort() != null;
        }

        /// <summary>将范围目标沿远离施法者的方向推开。</summary>
        public bool TryPushEnemies(
            IReadOnlyList<IDamageable> targets,
            Vector3 sourcePosition,
            float impulseStrength)
        {
            if (targets == null)
            {
                return false;
            }

            float strength = Mathf.Max(0f, impulseStrength);
            bool affected = false;
            for (int index = 0; index < targets.Count; index++)
            {
                IDamageable target = targets[index];
                if (!(target is IEnemyMotionEffectTarget motionTarget))
                {
                    continue;
                }

                Vector3 direction = target.Position - sourcePosition;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = Vector3.up;
                }

                motionTarget.AddExternalImpulse(direction.normalized * strength);
                affected = true;
            }

            return affected;
        }

        /// <summary>向范围目标提交按来源保存的永久速度倍率。</summary>
        public bool TrySlowEnemies(
            IReadOnlyList<IDamageable> targets,
            float multiplier,
            int sourceInstanceId)
        {
            if (targets == null)
            {
                return false;
            }

            float speedMultiplier = Mathf.Max(0f, multiplier);
            bool affected = false;
            for (int index = 0; index < targets.Count; index++)
            {
                if (!(targets[index] is IEnemyMotionEffectTarget motionTarget))
                {
                    continue;
                }

                if (_buffService != null && motionTarget is IBuffTarget buffTarget)
                {
                    // 正式运行时永久效果只进入场景级 BuffService，不在 Enemy 或本服务中保存目标状态。
                    _buffService.SetPermanentMovementSpeedMultiplier(
                        buffTarget,
                        speedMultiplier,
                        sourceInstanceId);
                    _permanentSourceInstanceIds.Add(sourceInstanceId);
                }
                else if (motionTarget is IPermanentEnemyMotionEffectTarget permanentTarget)
                {
                    // 没有容器的旧编辑器测试仍走兼容入口。
                    permanentTarget.SetPermanentMovementSpeedMultiplier(
                        speedMultiplier,
                        sourceInstanceId);
                    TrackLegacyPermanentSlow(permanentTarget, sourceInstanceId);
                }
                else
                {
                    motionTarget.SetMovementSpeedMultiplier(speedMultiplier);
                }
                affected = true;
            }

            return affected;
        }

        /// <summary>只向全局发射器效果边界提交倍率，具体攻击系统自行消费。</summary>
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

        /// <summary>对有效敌人提交配置的一个或两个限时 Buff。</summary>
        public int ApplyBuffs(
            IReadOnlyList<IDamageable> targets,
            BuffDefinitionSO primary,
            BuffDefinitionSO secondary,
            int sourceInstanceId)
        {
            if (targets == null || (primary == null && secondary == null) || _buffService == null)
            {
                return 0;
            }

            int affectedCount = 0;
            for (int index = 0; index < targets.Count; index++)
            {
                IDamageable target = targets[index];
                if (target?.Transform == null || !target.IsAlive)
                {
                    continue;
                }

                IBuffTarget buffTarget = target.Transform.GetComponentInParent<IBuffTarget>();
                if (buffTarget == null || !buffTarget.IsActive || !buffTarget.IsAlive ||
                    ((primary != null && primary.Polarity == BuffPolarity.Harmful ||
                      secondary != null && secondary.Polarity == BuffPolarity.Harmful) &&
                     !buffTarget.CanReceiveHarmfulEffects))
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

        /// <summary>选择生命值最高的敌人集火，并在相同时限内翻倍发射器攻击伤害。</summary>
        public bool TryFocusHighestHealth(
            IReadOnlyList<IDamageable> targets,
            float durationSeconds,
            int sourceInstanceId)
        {
            if (targets == null || durationSeconds <= 0f || _transmitterEffects == null)
            {
                return false;
            }

            Enemy highestHealthEnemy = null;
            for (int index = 0; index < targets.Count; index++)
            {
                IDamageable target = targets[index];
                Enemy enemy = target?.Transform != null
                    ? target.Transform.GetComponentInParent<Enemy>()
                    : null;
                if (enemy != null && !enemy.IsDead &&
                    (highestHealthEnemy == null || enemy.CurrentHealth > highestHealthEnemy.CurrentHealth))
                {
                    highestHealthEnemy = enemy;
                }
            }

            if (highestHealthEnemy == null)
            {
                return false;
            }

            _transmitterEffects.SetFocusedTarget(
                highestHealthEnemy,
                durationSeconds,
                sourceInstanceId);
            _transmitterEffects.SetAttackDamageMultiplier(
                FocusedAttackDamageMultiplier,
                sourceInstanceId,
                durationSeconds);
            return true;
        }

        /// <summary>清理本服务提交的按来源永久减速。</summary>
        public void ClearPermanentEffects()
        {
            if (_buffService != null)
            {
                foreach (int sourceInstanceId in _permanentSourceInstanceIds)
                {
                    _buffService.ClearPermanentMovementSpeedMultipliersBySourceId(sourceInstanceId);
                }
            }

            for (int index = _legacyPermanentSlowTargets.Count - 1; index >= 0; index--)
            {
                PermanentSlowTarget entry = _legacyPermanentSlowTargets[index];
                entry.Target.ClearPermanentMovementSpeedMultiplier(entry.SourceInstanceId);
            }

            _permanentSourceInstanceIds.Clear();
            _legacyPermanentSlowTargets.Clear();
        }

        /// <summary>记录旧兼容目标的来源组合，避免清理时重复提交。</summary>
        private void TrackLegacyPermanentSlow(
            IPermanentEnemyMotionEffectTarget target,
            int sourceInstanceId)
        {
            for (int index = 0; index < _legacyPermanentSlowTargets.Count; index++)
            {
                PermanentSlowTarget entry = _legacyPermanentSlowTargets[index];
                if (ReferenceEquals(entry.Target, target) && entry.SourceInstanceId == sourceInstanceId)
                {
                    return;
                }
            }

            _legacyPermanentSlowTargets.Add(new PermanentSlowTarget(target, sourceInstanceId));
        }

        private readonly struct PermanentSlowTarget
        {
            public IPermanentEnemyMotionEffectTarget Target { get; }
            public int SourceInstanceId { get; }

            public PermanentSlowTarget(
                IPermanentEnemyMotionEffectTarget target,
                int sourceInstanceId)
            {
                Target = target;
                SourceInstanceId = sourceInstanceId;
            }
        }
    }
}
