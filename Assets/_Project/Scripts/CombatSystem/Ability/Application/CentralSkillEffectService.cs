using System.Collections.Generic;
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
        private readonly CentralCore _centralCore;
        private readonly ITransmitterGlobalEffectService _transmitterEffects;
        private readonly List<PermanentSlowTarget> _permanentSlowTargets = new();

        /// <summary>注入当前中央核心和发射器全局效果状态。</summary>
        public CentralSkillEffectService(
            CentralCore centralCore,
            ITransmitterGlobalEffectService transmitterEffects)
        {
            _centralCore = centralCore;
            _transmitterEffects = transmitterEffects;
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

                if (motionTarget is IPermanentEnemyMotionEffectTarget permanentTarget)
                {
                    permanentTarget.SetPermanentMovementSpeedMultiplier(
                        speedMultiplier,
                        sourceInstanceId);
                    TrackPermanentSlow(permanentTarget, sourceInstanceId);
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
        public bool TrySetTransmitterAttackSpeedMultiplier(
            float multiplier,
            int sourceInstanceId)
        {
            if (_transmitterEffects == null)
            {
                return false;
            }

            _transmitterEffects.SetAttackSpeedMultiplier(
                Mathf.Max(0f, multiplier),
                sourceInstanceId);
            return true;
        }

        /// <summary>清理本服务提交的按来源永久减速。</summary>
        public void ClearPermanentEffects()
        {
            for (int index = _permanentSlowTargets.Count - 1; index >= 0; index--)
            {
                PermanentSlowTarget entry = _permanentSlowTargets[index];
                entry.Target.ClearPermanentMovementSpeedMultiplier(entry.SourceInstanceId);
            }

            _permanentSlowTargets.Clear();
        }

        /// <summary>记录尚未记录的目标来源组合，避免清理时重复提交。</summary>
        private void TrackPermanentSlow(
            IPermanentEnemyMotionEffectTarget target,
            int sourceInstanceId)
        {
            for (int index = 0; index < _permanentSlowTargets.Count; index++)
            {
                PermanentSlowTarget entry = _permanentSlowTargets[index];
                if (ReferenceEquals(entry.Target, target) && entry.SourceInstanceId == sourceInstanceId)
                {
                    return;
                }
            }

            _permanentSlowTargets.Add(new PermanentSlowTarget(target, sourceInstanceId));
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
