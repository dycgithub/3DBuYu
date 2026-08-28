using Interfaces;

namespace CombatSystem
{
    /// <summary>统一提交伤害请求，避免攻击入口直接操作目标生命值。</summary>
    public sealed class DamagePipeline : IDamageApplier
    {
        public bool TryApply(IDamageable target, in DamageRequest request, out DamageResult result)
        {
            if (target == null || !target.IsAlive)
            {
                result = new DamageResult { Outcome = DamageOutcome.Invalid };
                return false;
            }

            if (target is IDamageReceiver receiver)
            {
                result = receiver.ReceiveDamage(request);
                return result.WasApplied;
            }

            target.TakeDamage(request.BaseDamage);
            result = new DamageResult
            {
                Outcome = DamageOutcome.Applied,
                ActualDamage = request.BaseDamage,
                RemainingHealth = 0f,
                IsKill = !target.IsAlive
            };
            return true;
        }
    }
}
