namespace CombatSystem
{
    /// <summary>
    /// 表示伤害来源的属性类型；具体抗性规则由伤害接收者实现。
    /// </summary>
    public enum DamageType
    {
        /// <summary>物理伤害。</summary>
        Physical = 0,

        /// <summary>火焰伤害。</summary>
        Fire = 1,

        /// <summary>冰霜伤害。</summary>
        Ice = 2,

        /// <summary>雷电伤害。</summary>
        Lightning = 3,

        /// <summary>不受常规属性抗性影响的纯粹伤害。</summary>
        Pure = 4
    }
}
