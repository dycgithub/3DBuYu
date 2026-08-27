using Interfaces;

namespace CombatSystem
{
    /// <summary>
    /// 伤害系统的统一入口，避免子弹直接依赖 Enemy。
    /// </summary>
    public sealed class DamagePipeline : IDamageApplier
    {
        public bool TryApply(
            IDamageable target,
            in DamageRequest request,
            out DamageResult result)
        {
            if (target == null || !target.IsAlive)
            {
                result = new DamageResult
                {
                    Outcome = DamageOutcome.Invalid
                };
                return false;
            }

            if (target is IDamageReceiver receiver)
            {
                result = receiver.ReceiveDamage(request);
                return result.Outcome == DamageOutcome.Applied ||
                       result.Outcome == DamageOutcome.Killed;
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
