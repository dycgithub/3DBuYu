using System;
using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>管理已装备卡牌与本局来源状态；卸装只移出释放索引，不清空次数和冷却。</summary>
    public sealed class SkillHand
    {
        private readonly List<SkillCardState> _skills = new();
        private readonly Dictionary<int, SkillCardState> _cardsById = new();
        private readonly Dictionary<int, SkillCardState> _retainedBySource = new();

        public IReadOnlyList<SkillCardState> Skills { get; }
        public event Action SkillsChanged;
        public event Action<SkillCardState> SkillStateChanged;

        /// <summary>建立只读的显示顺序视图，避免外部绕过卡牌索引修改集合。</summary>
        public SkillHand()
        {
            Skills = _skills.AsReadOnly();
        }

        /// <summary>按装备快照更新当前手牌，并保留本局已经授予的来源状态。</summary>
        public void Rebuild(IEnumerable<SkillCardState> cards)
        {
            _skills.Clear();
            _cardsById.Clear();
            if (cards != null)
            {
                foreach (SkillCardState card in cards)
                {
                    if (card == null || _cardsById.ContainsKey(card.CardId))
                    {
                        continue;
                    }
                    _skills.Add(card);
                    _cardsById.Add(card.CardId, card);
                    _retainedBySource[card.SourceItemInstanceId] = card;
                }
            }
            SkillsChanged?.Invoke();
        }

        /// <summary>从当前可释放卡牌中查找实例。</summary>
        public SkillCardState FindByCardId(int cardId)
        {
            return _cardsById.TryGetValue(cardId, out SkillCardState card) ? card : null;
        }

        /// <summary>按物品实例找回本局状态，包括已经卸装的卡牌。</summary>
        public SkillCardState FindBySourceItemInstanceId(int sourceItemInstanceId)
        {
            return _retainedBySource.TryGetValue(sourceItemInstanceId, out SkillCardState card) ? card : null;
        }

        /// <summary>从当前手牌移除卡牌，保留本局来源记录。</summary>
        public bool RemoveByCardId(int cardId)
        {
            if (!_cardsById.Remove(cardId, out SkillCardState card))
            {
                return false;
            }
            _skills.Remove(card);
            SkillsChanged?.Invoke();
            return true;
        }

        /// <summary>按来源物品移除当前卡牌。</summary>
        public bool RemoveBySourceItemInstanceId(int sourceItemInstanceId)
        {
            SkillCardState card = FindBySourceItemInstanceId(sourceItemInstanceId);
            return card != null && RemoveByCardId(card.CardId);
        }

        /// <summary>发布已装备卡牌的次数或冷却状态变化。</summary>
        public void NotifySkillStateChanged(SkillCardState card)
        {
            if (card != null && _cardsById.ContainsKey(card.CardId))
            {
                SkillStateChanged?.Invoke(card);
            }
        }

        /// <summary>推进全部来源的冷却，仅在冷却结束时通知表现；回调放到遍历完成后。</summary>
        public void AdvanceCooldowns(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            List<SkillCardState> completed = null;
            foreach (SkillCardState card in _retainedBySource.Values)
            {
                bool wasOnCooldown = card.IsOnCooldown;
                card.AdvanceCooldown(deltaTime);
                if (wasOnCooldown && !card.IsOnCooldown)
                {
                    completed ??= new List<SkillCardState>();
                    completed.Add(card);
                }
            }

            if (completed != null)
            {
                foreach (SkillCardState card in completed)
                {
                    NotifySkillStateChanged(card);
                }
            }
        }

        /// <summary>在本局结束或开始新局时清理全部来源记录。</summary>
        public void Clear()
        {
            _retainedBySource.Clear();
            _skills.Clear();
            _cardsById.Clear();
            SkillsChanged?.Invoke();
        }
    }
}
