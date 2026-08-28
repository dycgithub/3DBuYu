namespace CombatSystem
{
    public interface ITransmitterAttackService
    {
        bool TryExecute(in TransmitterAttackInput input);
    }
}
