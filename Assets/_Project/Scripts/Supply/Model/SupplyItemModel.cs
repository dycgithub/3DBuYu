using System;
using System.Collections.Generic;

namespace Supply
{
    /// <summary>提供可重复注入的整数和浮点随机值。</summary>
    public interface IRandomSource
    {
        /// <summary>返回大于等于零且小于指定上限的整数。</summary>
        /// <param name="exclusiveMax">随机上限，不包含该值。</param>
        /// <returns>随机整数。</returns>
        int NextInt(int exclusiveMax);

        /// <summary>返回一个随机浮点值。</summary>
        /// <returns>随机浮点值。</returns>
        float NextFloat();
    }

    /// <summary>定义物品可参与的莫比乌斯职责池。</summary>
    [Flags]
    public enum SupplyItemRole
    {
        /// <summary>没有职责。</summary>
        None = 0,

        /// <summary>火力职责。</summary>
        Firepower = 1,

        /// <summary>弹体职责。</summary>
        Projectile = 2,

        /// <summary>技能职责。</summary>
        Skill = 4,

        /// <summary>所有职责。</summary>
        All = Firepower | Projectile | Skill
    }

    /// <summary>描述一个可被 Supply 抽取的物品及其权重。</summary>
    public sealed class SupplyItemEntry
    {
        /// <summary>物品稳定标识。</summary>
        public string ItemId { get; }

        /// <summary>参与加权抽取的正整数权重。</summary>
        public int Weight { get; }

        /// <summary>物品所属职责集合。</summary>
        public SupplyItemRole Role { get; }

        /// <summary>创建一个物品池条目。</summary>
        /// <param name="itemId">物品稳定标识。</param>
        /// <param name="weight">抽取权重。</param>
        /// <param name="role">物品职责集合。</param>
        public SupplyItemEntry(string itemId, int weight, SupplyItemRole role)
        {
            ItemId = itemId ?? string.Empty;
            Weight = Math.Max(0, weight);
            Role = role;
        }

        /// <summary>判断条目是否属于指定职责池。</summary>
        /// <param name="role">待匹配职责。</param>
        /// <returns>条目可用于该职责池时返回 true。</returns>
        public bool Supports(SupplyItemRole role)
        {
            return role == SupplyItemRole.All ||
                   Role == SupplyItemRole.All ||
                   (Role & role) != 0;
        }
    }

    /// <summary>保存 Supply 的物品条目，并提供统一的加权抽取规则。</summary>
    public sealed class SupplyItemPool
    {
        private readonly IReadOnlyList<SupplyItemEntry> _entries;

        /// <summary>池中的物品条目。</summary>
        public IReadOnlyList<SupplyItemEntry> Entries => _entries;

        /// <summary>使用只读条目集合创建物品池。</summary>
        /// <param name="entries">已复制的物品条目。</param>
        private SupplyItemPool(IReadOnlyList<SupplyItemEntry> entries)
        {
            _entries = entries;
        }

        /// <summary>从条目数组创建物品池。</summary>
        /// <param name="entries">物品条目。</param>
        /// <returns>新的物品池。</returns>
        public static SupplyItemPool Create(params SupplyItemEntry[] entries)
        {
            return Create((IEnumerable<SupplyItemEntry>)entries);
        }

        /// <summary>从条目集合创建物品池。</summary>
        /// <param name="entries">物品条目集合。</param>
        /// <returns>新的物品池。</returns>
        public static SupplyItemPool Create(IEnumerable<SupplyItemEntry> entries)
        {
            var copied = new List<SupplyItemEntry>();
            if (entries != null)
            {
                foreach (SupplyItemEntry entry in entries)
                {
                    if (entry != null)
                    {
                        copied.Add(entry);
                    }
                }
            }

            return new SupplyItemPool(copied.AsReadOnly());
        }

        /// <summary>判断指定职责是否存在有效物品。</summary>
        /// <param name="role">待检查职责。</param>
        /// <returns>存在正权重条目时返回 true。</returns>
        public bool HasEntries(SupplyItemRole role)
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].Supports(role) && _entries[index].Weight > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>按职责和权重抽取一个条目。</summary>
        /// <param name="random">随机源。</param>
        /// <param name="role">待抽取职责。</param>
        /// <param name="entry">抽到的条目。</param>
        /// <returns>存在可抽取条目时返回 true。</returns>
        public bool TryDraw(IRandomSource random, SupplyItemRole role, out SupplyItemEntry entry)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var candidates = new List<SupplyItemEntry>();
            int totalWeight = 0;
            for (int index = 0; index < _entries.Count; index++)
            {
                SupplyItemEntry candidate = _entries[index];
                if (!candidate.Supports(role) || candidate.Weight <= 0)
                {
                    continue;
                }

                candidates.Add(candidate);
                totalWeight = AddWeight(totalWeight, candidate.Weight);
            }

            if (totalWeight <= 0)
            {
                entry = null;
                return false;
            }

            int roll = random.NextInt(totalWeight);
            for (int index = 0; index < candidates.Count; index++)
            {
                SupplyItemEntry candidate = candidates[index];
                if (roll < candidate.Weight)
                {
                    entry = candidate;
                    return true;
                }

                roll -= candidate.Weight;
            }

            entry = candidates[candidates.Count - 1];
            return true;
        }

        /// <summary>安全累加权重，避免无效配置溢出。</summary>
        /// <param name="current">当前总权重。</param>
        /// <param name="weight">待加入权重。</param>
        /// <returns>限制在整数范围内的总权重。</returns>
        private static int AddWeight(int current, int weight)
        {
            if (weight > int.MaxValue - current)
            {
                return int.MaxValue;
            }

            return current + weight;
        }
    }

    /// <summary>描述一次 Supply 奖励及其确定后的强度等级。</summary>
    public sealed class SupplyRewardItem
    {
        /// <summary>奖励物品稳定标识。</summary>
        public string ItemId { get; }

        /// <summary>奖励实例的强度等级。</summary>
        public int PowerLevel { get; }

        /// <summary>创建一项 Supply 奖励。</summary>
        /// <param name="itemId">物品稳定标识。</param>
        /// <param name="powerLevel">物品强度等级。</param>
        public SupplyRewardItem(string itemId, int powerLevel)
        {
            ItemId = itemId ?? string.Empty;
            PowerLevel = ItemPowerTable.NormalizeLevel(powerLevel);
        }
    }
}
