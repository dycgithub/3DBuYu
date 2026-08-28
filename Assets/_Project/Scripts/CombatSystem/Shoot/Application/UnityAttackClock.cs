namespace CombatSystem
{
    public sealed class UnityAttackClock : IAttackClock
    {
        public float Time => UnityEngine.Time.time;
    }
}
