namespace ShootingSystem.Buffs
{
    /// <summary>
    /// 通用数值型 Buff：不附带专属逻辑，纯粹承载 BuffConfig.Value 数值，
    /// 供消费方通过 BuffController.GetModifier(BuffType) 读取
    /// （如弹药的攻击力加成、射程、暴击、弹射等）。
    /// </summary>
    public class StatBuff : BuffBase
    {
        public override void OnApply() { }
        public override void OnExpire() { }
    }
}
