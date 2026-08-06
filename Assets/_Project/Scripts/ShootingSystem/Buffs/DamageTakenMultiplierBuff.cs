namespace ShootingSystem.Buffs
{
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
