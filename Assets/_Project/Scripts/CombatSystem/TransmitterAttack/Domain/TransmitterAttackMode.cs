namespace CombatSystem
{
    /// <summary>发射器当前使用的唯一攻击输入模式。</summary>
    public enum TransmitterAttackMode
    {
        /// <summary>自动跟随分配目标并持续攻击。</summary>
        Automatic,

        /// <summary>按端口对应的数字键攻击。</summary>
        Key,

        /// <summary>按住鼠标左键攻击光标命中的目标。</summary>
        Mouse,

        /// <summary>点击敌人后持续让所有发射器攻击该目标。</summary>
        MarkedTarget
    }
}
