using Interfaces;

namespace CombatSystem
{
    /// <summary>投射物提交伤害请求的端口。</summary>
    public interface IDamageApplier
    {
        bool TryApply(
            IDamageable target,
            in DamageRequest request,
            out DamageResult result);
    }
}
