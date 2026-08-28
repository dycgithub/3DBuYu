namespace CombatSystem
{
    public sealed class SkillCardRuntime
    {
        public int SourceItemInstanceId { get; }
        public string SourceItemId { get; }
        public SkillDefinition Definition { get; }

        public SkillCardRuntime(int sourceItemInstanceId, string sourceItemId, SkillDefinition definition)
        {
            SourceItemInstanceId = sourceItemInstanceId;
            SourceItemId = sourceItemId;
            Definition = definition;
        }
    }
}
