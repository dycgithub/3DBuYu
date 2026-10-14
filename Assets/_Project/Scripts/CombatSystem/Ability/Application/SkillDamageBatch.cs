using System.Collections.Generic;
using System;
using Interfaces;

namespace CombatSystem
{
    /// <summary>暂存整卡伤害、投射物和视效；来源消费成功后才提交不可回滚效果。</summary>
    internal sealed class SkillDamageBatch : IDamageRequestProcessor, IDisposable
    {
        private readonly int _sourceId;
        private readonly List<(IDamageable Target, DamageRequest Request)> _requests = new();
        private readonly List<Action> _visualEffects = new();
        private readonly List<Action> _actions = new();
        private readonly List<IPreparedProjectileBatch> _projectiles = new();
        private bool _requiresProjectilePreparation;
        internal int Count => _requests.Count;
        internal bool RequiresProjectilePreparation => _requiresProjectilePreparation;

        /// <summary>来源消费事务必须先准备完整投射物，不接受可能在消费后拒绝的逐弹调用。</summary>
        internal static SkillDamageBatch CreateTransaction(int sourceId)
        {
            return new SkillDamageBatch(sourceId) { _requiresProjectilePreparation = true };
        }

        /// <summary>保存完整准备的投射物；未提交的批次随整卡失败一起取消。</summary>
        internal void DeferProjectiles(IPreparedProjectileBatch projectiles)
        {
            _projectiles.Add(projectiles);
        }

        /// <summary>暂存已经校验的发射动作，准备失败或来源消费失败时不执行。</summary>
        internal void DeferAction(Action action)
        {
            _actions.Add(action);
        }

        /// <summary>暂存技能视效，等整张卡提交后再播放。</summary>
        internal void DeferVisualEffect(Action visualEffect)
        {
            if (visualEffect != null)
            {
                _visualEffects.Add(visualEffect);
            }
        }

        /// <summary>绑定整张卡唯一的来源物品身份。</summary>
        internal SkillDamageBatch(int sourceId) => _sourceId = sourceId;

        /// <summary>接收请求而不修改目标；返回结果只供准备阶段统计接收数量。</summary>
        public bool TryApplyDamage(IDamageable target, in DamageRequest request, out DamageResult result)
        {
            if (target == null)
            {
                result = new DamageResult { Outcome = DamageOutcome.Invalid };
                return false;
            }

            DamageRequest normalized = request;
            normalized.SourceInstanceId = _sourceId;
            _requests.Add((target, normalized));
            result = new DamageResult { Outcome = DamageOutcome.Applied };
            return true;
        }

        /// <summary>依配置顺序提交伤害；单个目标拒绝伤害不撤销已经生效的请求。</summary>
        internal int Commit(IDamageRequestProcessor processor)
        {
            foreach (IPreparedProjectileBatch projectiles in _projectiles)
            {
                projectiles.Commit();
            }
            foreach (Action action in _actions)
            {
                action();
            }
            _actions.Clear();

            int appliedCount = 0;
            if (processor != null)
            {
                foreach (var pending in _requests)
                {
                    if (processor.TryApplyDamage(pending.Target, pending.Request, out DamageResult result) && result.WasApplied)
                    {
                        appliedCount++;
                    }
                }
            }

            foreach (Action visualEffect in _visualEffects)
            {
                visualEffect();
            }

            _visualEffects.Clear();
            return appliedCount;
        }

        /// <summary>消费失败、准备失败或异常退出时取消尚未提交的投射物。</summary>
        public void Dispose()
        {
            foreach (IPreparedProjectileBatch projectiles in _projectiles)
            {
                projectiles.Dispose();
            }
            _projectiles.Clear();
            _actions.Clear();
            _requests.Clear();
            _visualEffects.Clear();
        }
    }
}
