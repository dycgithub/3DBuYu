using Interfaces;

namespace CombatSystem
{
    public interface IDamageApplier
    {
        bool TryApply(IDamageable target, in DamageRequest request, out DamageResult result);
    }
}
