namespace CombatSystem
{
    /// <summary>Unity 时间适配器，避免冷却规则直接依赖静态 Time。</summary>
    public sealed class UnityAttackClock : IAttackClock
    {
        public float Time => UnityEngine.Time.time;
    }
}
