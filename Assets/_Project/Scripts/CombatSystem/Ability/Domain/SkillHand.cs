using System;
using System.Collections.Generic;

namespace CombatSystem
{
    public sealed class SkillHand
    {
        private readonly List<SkillCardRuntime> _cards = new();

        public IReadOnlyList<SkillCardRuntime> Cards => _cards;
        public event Action Changed;

        public void SetCards(IEnumerable<SkillCardRuntime> cards)
        {
            _cards.Clear();
            if (cards != null)
            {
                foreach (SkillCardRuntime card in cards)
                {
                    if (card?.Definition != null)
                        _cards.Add(card);
                }
            }
            Changed?.Invoke();
        }

        public SkillCardRuntime FindBySourceItem(int sourceItemInstanceId)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].SourceItemInstanceId == sourceItemInstanceId)
                    return _cards[i];
            }
            return null;
        }

        public bool RemoveBySourceItem(int sourceItemInstanceId)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].SourceItemInstanceId != sourceItemInstanceId)
                    continue;
                _cards.RemoveAt(i);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public void Clear()
        {
            if (_cards.Count == 0)
                return;
            _cards.Clear();
            Changed?.Invoke();
        }
    }
}
