namespace CombatSystem
{
    public interface IBuffable
    {
        void ApplyBuff(BuffDefinition definition);
        void ApplyBuff(BuffDefinition definition, int sourceId);
    }
}
