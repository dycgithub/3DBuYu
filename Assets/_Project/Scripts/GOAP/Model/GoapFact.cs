using System;

namespace EnemySystem.GOAP
{
    /// <summary>GOAP 世界状态中可组合的事实。</summary>
    [Flags]
    public enum GoapFact : ulong
    {
        None = 0,
        PreyAvailable = 1UL << 0,
        PreyInRange = 1UL << 1,
        PreyConsumed = 1UL << 2,
        PlayerAvailable = 1UL << 3,
        PlayerInRange = 1UL << 4,
        ChildPopulationBelowTarget = 1UL << 5,
        ChildSlotAvailable = 1UL << 6,
        SpawnReady = 1UL << 7,
        ChildPopulationSatisfied = 1UL << 8
    }
}
