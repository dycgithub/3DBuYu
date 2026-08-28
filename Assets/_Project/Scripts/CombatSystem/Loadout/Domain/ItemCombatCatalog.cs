namespace CombatSystem
{
    public sealed class ItemCombatCatalog
    {
        public ItemCombatDefinition Resolve(ItemDefinition definition)
        {
            return definition?.CombatDefinition != null
                ? definition.CombatDefinition
                : ItemCombatDefinition.Default;
        }

        public SkillDefinition ResolveCentralSkill(ItemDefinition definition)
        {
            ItemCombatDefinition combat = Resolve(definition);
            if (!combat.AppliesToCentral)
                return null;

            return combat.CentralSkill != null
                ? combat.CentralSkill
                : SkillDefinition.CreateRuntime(
                    string.IsNullOrEmpty(definition?.Id) ? "central-sweep" : definition.Id + "-central-sweep",
                    combat.CentralDamage);
        }
    }
}
