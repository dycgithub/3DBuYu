namespace ItemSystem.Functions
{
    /// <summary>
    /// 核心技能：解锁所有炮台插槽。
    /// 可被 SkillDecorator 包裹扩展（冷却、连发等）。
    /// </summary>
    public class UnlockAllPortsSkill : SkillBase
    {
        public override string Name => "Unlock All Ports";
        public override string Description => "Unlocks every locked turret port.";

        protected override void OnActivate(IItemActivationContext context) => context.UnlockAllPorts();
    }
}
