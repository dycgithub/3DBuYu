namespace CombatSystem
{
    /// <summary>一次攻击参数修改规则的运行时契约。</summary>
    public interface IAttackModifier
    {
        void Modify(ref AttackInfo info);
    }
}
