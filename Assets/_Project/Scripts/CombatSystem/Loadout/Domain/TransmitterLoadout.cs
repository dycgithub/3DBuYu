using System.Collections.Generic;

namespace CombatSystem
{
    public sealed class TransmitterLoadout : ITransmitterShootModifierSource
    {
        private readonly ItemCombatCatalog _catalog;
        private readonly Dictionary<int, List<ItemVM>> _items = new();

        public TransmitterLoadout(ItemCombatCatalog catalog)
        {
            _catalog = catalog;
        }

        public void BeginRun() => Clear();

        public void SetItems(int transmitterIndex, IEnumerable<ItemVM> items)
        {
            var replacement = new List<ItemVM>();
            if (items != null)
            {
                foreach (ItemVM item in items)
                {
                    if (item != null)
                        replacement.Add(item);
                }
            }

            if (replacement.Count == 0)
                _items.Remove(transmitterIndex);
            else
                _items[transmitterIndex] = replacement;
        }

        public float GetDamageBonus(int transmitterIndex)
        {
            TransmitterShootModifiers modifiers = TransmitterShootModifiers.Default;
            Collect(transmitterIndex, ref modifiers);
            return modifiers.DamageBonus;
        }

        public void Collect(int transmitterIndex, ref TransmitterShootModifiers modifiers)
        {
            if (!_items.TryGetValue(transmitterIndex, out List<ItemVM> items))
                return;

            for (int i = 0; i < items.Count; i++)
            {
                ItemCombatDefinition combat = _catalog.Resolve(items[i].Definition);
                if (!combat.AppliesToTransmitter)
                    continue;

                modifiers.DamageBonus += combat.TransmitterDamageBonus;
                TransmitterShootModifierDefinition[] definitions = combat.TransmitterModifiers;
                if (definitions == null)
                    continue;
                for (int modifierIndex = 0; modifierIndex < definitions.Length; modifierIndex++)
                    definitions[modifierIndex]?.Apply(ref modifiers);
            }
        }

        public void Clear() => _items.Clear();
    }
}
