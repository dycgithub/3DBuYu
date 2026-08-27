namespace CombatSystem
{
    /// <summary>记录受到伤害倍率的 Buff 运行时实例。</summary>
    public class DamageTakenMultiplierBuff : BuffBase
    {
        public override void OnApply()
        {
            UnityEngine.Debug.Log($"[Buff] DamageTakenMultiplier x{Config.Value} applied for {Config.Duration}s");
        }

        public override void OnExpire()
        {
            UnityEngine.Debug.Log("[Buff] DamageTakenMultiplier expired");
        }
    }
}
