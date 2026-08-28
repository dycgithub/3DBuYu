namespace CombatSystem
{
    public interface ICombatItemConsumer
    {
        bool CanConsume(int itemInstanceId);
        bool TryConsume(int itemInstanceId);
    }
}
