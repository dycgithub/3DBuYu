namespace CombatSystem
{
    public interface IAttackCommandSource
    {
        bool TryGetAimCommand(int transmitterIndex, out AimCommand command);
    }
}
