using Interfaces;

namespace CombatSystem
{
    /// <summary>
    /// 统一提交伤害请求；目标必须实现完整的伤害请求接收契约，不提供旧式回退。
    /// </summary>
    public sealed class DamageRequestProcessor : IDamageRequestProcessor
    {
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
            return result.WasApplied;
        }
    }
}
