using System;
using Interfaces;

namespace CombatSystem
{
    /// <summary>
    /// 统一提交伤害请求，并发布实际扣血结果。
    /// </summary>
    public sealed class DamageRequestProcessor : IDamageRequestProcessor, IDamageEventSource
    {
        /// <summary>目标实际扣除生命后触发。</summary>
        public event Action<DamageEvent> DamageApplied;

        /// <summary>先验证完整目标契约和存活状态，再处理一次伤害请求。</summary>
        public bool TryApplyDamage(IDamageable target, in DamageRequest request, out DamageResult result)
        {
            if (target is not IDamageRequestReceiver receiver || !target.IsAliveAndValid())
            {
                result = new DamageResult
                {
                    Outcome = DamageOutcome.Invalid
                };
                return false;
            }

            result = receiver.ReceiveDamageRequest(request);
            if (!result.WasApplied)
            {
                return false;
            }

            DamageApplied?.Invoke(new DamageEvent(target, request, result));
            return true;
        }
    }
}
