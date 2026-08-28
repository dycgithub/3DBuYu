using System.Collections.Generic;

namespace CombatSystem
{
    public sealed class CentralLoadout
    {
        private readonly ItemCombatCatalog _catalog;
        private readonly SkillHand _hand;
        private readonly List<int> _itemInstanceIds = new();

        public SkillHand Hand => _hand;
        public IReadOnlyList<int> ItemInstanceIds => _itemInstanceIds;

        public CentralLoadout(ItemCombatCatalog catalog, SkillHand hand)
        {
            _catalog = catalog;
            _hand = hand;
        }

        public void BeginRun() => Clear();

        public void SetItems(IEnumerable<ItemVM> items)
        {
            _itemInstanceIds.Clear();
            var cards = new List<SkillCardRuntime>();
            if (items != null)
            {
                foreach (ItemVM item in items)
                {
                    if (item == null)
                        continue;

                    ItemCombatDefinition combat = _catalog.Resolve(item.Definition);
                    if (!combat.AppliesToCentral)
                        continue;

                    SkillDefinition skill = _catalog.ResolveCentralSkill(item.Definition);
                    if (skill == null)
                        continue;

                    _itemInstanceIds.Add(item.InstanceId);
                    cards.Add(new SkillCardRuntime(item.InstanceId, item.Definition?.Id, skill));
                }
            }
            _hand.SetCards(cards);
        }

        public void Clear()
        {
            _itemInstanceIds.Clear();
            _hand.Clear();
        }
    }
}
