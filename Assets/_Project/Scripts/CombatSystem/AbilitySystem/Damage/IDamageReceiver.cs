namespace CombatSystem
{
    /// <summary>
    /// 结构化伤害接收契约。具体目标不需要依赖 ProjectileSimulationService。
    /// </summary>
    public interface IDamageReceiver
    {
        DamageResult ReceiveDamage(in DamageRequest request);
    }
}
