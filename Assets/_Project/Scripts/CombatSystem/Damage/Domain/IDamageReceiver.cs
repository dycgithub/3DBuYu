namespace CombatSystem
{
    public interface IDamageReceiver
    {
        DamageResult ReceiveDamage(in DamageRequest request);
    }
}
