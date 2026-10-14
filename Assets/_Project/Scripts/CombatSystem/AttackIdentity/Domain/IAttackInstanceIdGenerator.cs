namespace CombatSystem
{
    /// <summary>
    /// 为所有攻击来源分配进程内唯一的攻击实例标识。
    /// </summary>
    public interface IAttackInstanceIdGenerator
    {
        /// <summary>分配下一个攻击实例标识。</summary>
        /// <returns>大于零的攻击实例标识。</returns>
        int Next();
    }
}
