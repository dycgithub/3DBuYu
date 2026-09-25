using System;
using System.Collections.Generic;
using Services;

namespace Supply
{
    /// <summary>表示莫比乌斯奖池中的一个独立候选槽位。</summary>
    public sealed class MobiusCandidate
    {
        /// <summary>候选槽位身份。</summary>
        public string CandidateId { get; }

        /// <summary>候选对应的物品标识。</summary>
        public string ItemId { get; }

        /// <summary>候选的原始物品权重。</summary>
        public int Weight { get; }

        /// <summary>创建一个莫比乌斯候选。</summary>
        /// <param name="candidateId">候选槽位身份。</param>
        /// <param name="itemId">物品稳定标识。</param>
        /// <param name="weight">物品原始权重。</param>
        public MobiusCandidate(string candidateId, string itemId, int weight)
        {
            CandidateId = candidateId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            Weight = Math.Max(0, weight);
        }
    }

    /// <summary>记录一次莫比乌斯候选抓取结果。</summary>
    public sealed class MobiusClaimResult
    {
        /// <summary>被抓取的候选槽位身份。</summary>
        public string CandidateId { get; }

        /// <summary>被抓取的物品标识。</summary>
        public string ItemId { get; }

        /// <summary>被抓取的奖励实例。</summary>
        public SupplyRewardItem Reward { get; }

        /// <summary>创建莫比乌斯抓取结果。</summary>
        /// <param name="candidateId">候选槽位身份。</param>
        /// <param name="itemId">物品稳定标识。</param>
        public MobiusClaimResult(string candidateId, string itemId)
        {
            CandidateId = candidateId;
            ItemId = itemId;
            Reward = new SupplyRewardItem(itemId, 0);
        }
    }

    /// <summary>管理按职责隔离的莫比乌斯候选、保底和积分消耗。</summary>
    public sealed class MobiusPoolService
    {
        private const int CandidateCount = 6;
        private const int ClaimCost = 100;
        private const int RepeatGuarantee = 3;

        private readonly IPointsService _points;
        private readonly SupplyItemPool _pool;
        private readonly IRandomSource _random;
        private readonly Dictionary<SupplyItemRole, PoolState> _states = new();
        private SupplyItemRole _selectedRole;
        private int _candidateSequence;
        private bool _isBusy;

        /// <summary>当前职责的候选槽位。</summary>
        public IReadOnlyList<MobiusCandidate> Candidates => GetState().ReadOnlyCandidates;

        /// <summary>当前职责连续抓取相同物品的次数。</summary>
        public int RepeatCount => GetState().RepeatCount;

        /// <summary>创建莫比乌斯奖池服务。</summary>
        /// <param name="points">积分服务。</param>
        /// <param name="pool">职责物品池。</param>
        /// <param name="random">可重复随机源。</param>
        public MobiusPoolService(IPointsService points, SupplyItemPool pool, IRandomSource random)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>选择职责并初始化该职责独立保存的候选槽位。</summary>
        /// <param name="role">待选择职责。</param>
        public void SelectRole(SupplyItemRole role)
        {
            if (!IsSingleRole(role))
            {
                return;
            }

            _selectedRole = role;
            PoolState state = GetState();
            if (state.Candidates.Count > 0)
            {
                return;
            }

            for (int index = 0; index < CandidateCount; index++)
            {
                SupplyItemEntry entry = PickEntry(state, false);
                if (entry == null)
                {
                    break;
                }

                state.AddCandidate(CreateCandidate(entry));
            }
        }

        /// <summary>抓取候选、扣费并补充一个新的候选槽位。</summary>
        /// <param name="candidateId">要抓取的候选槽位身份。</param>
        /// <param name="result">成功抓取后的结果。</param>
        /// <returns>抓取成功时返回 true。</returns>
        public bool TryClaim(string candidateId, out MobiusClaimResult result)
        {
            result = null;
            if (_isBusy)
            {
                return false;
            }

            _isBusy = true;
            try
            {
                return TryClaimInternal(candidateId, out result);
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>执行一次候选抓取的状态提交。</summary>
        /// <param name="candidateId">要抓取的候选槽位身份。</param>
        /// <param name="result">成功抓取后的结果。</param>
        /// <returns>抓取成功时返回 true。</returns>
        private bool TryClaimInternal(string candidateId, out MobiusClaimResult result)
        {
            result = null;
            PoolState state = GetState();
            MobiusCandidate candidate = FindCandidate(state, candidateId);
            if (candidate == null || IsBlockedByGuarantee(state, candidate) ||
                !_points.HasEnoughPoints(ClaimCost) ||
                !_points.SpendPoints(ClaimCost, "Supply 莫比乌斯"))
            {
                return false;
            }

            state.RemoveCandidate(candidate);
            bool wasOwned = state.OwnedItemIds.Contains(candidate.ItemId);
            state.RegisterClaim(candidate.ItemId, wasOwned);
            state.OwnedItemIds.Add(candidate.ItemId);
            result = new MobiusClaimResult(candidate.CandidateId, candidate.ItemId);

            SupplyItemEntry replacement = PickEntry(state, state.RepeatCount >= RepeatGuarantee);
            if (replacement != null)
            {
                state.AddCandidate(CreateCandidate(replacement));
            }

            return true;
        }

        /// <summary>判断职责是否为单一有效职责。</summary>
        /// <param name="role">待检查职责。</param>
        /// <returns>职责有效时返回 true。</returns>
        private static bool IsSingleRole(SupplyItemRole role)
        {
            return role == SupplyItemRole.Firepower
                || role == SupplyItemRole.Projectile
                || role == SupplyItemRole.Skill;
        }

        /// <summary>保底生效且仍有未拥有物品时，禁止抓取重复候选。</summary>
        /// <param name="state">当前职责状态。</param>
        /// <param name="candidate">待抓取候选。</param>
        /// <returns>候选被保底规则暂时禁止时返回 true。</returns>
        private bool IsBlockedByGuarantee(PoolState state, MobiusCandidate candidate)
        {
            return state.RepeatCount >= RepeatGuarantee
                && HasUnownedEntry(state)
                && state.OwnedItemIds.Contains(candidate.ItemId);
        }

        /// <summary>判断当前职责是否仍存在未拥有的有效物品。</summary>
        /// <param name="state">当前职责状态。</param>
        /// <returns>存在未拥有物品时返回 true。</returns>
        private bool HasUnownedEntry(PoolState state)
        {
            for (int index = 0; index < _pool.Entries.Count; index++)
            {
                SupplyItemEntry entry = _pool.Entries[index];
                if (entry.Supports(_selectedRole)
                    && entry.Weight > 0
                    && !state.OwnedItemIds.Contains(entry.ItemId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>读取当前职责对应的状态对象。</summary>
        /// <returns>当前职责状态。</returns>
        private PoolState GetState()
        {
            if (!_states.TryGetValue(_selectedRole, out PoolState state))
            {
                state = new PoolState();
                _states.Add(_selectedRole, state);
            }

            return state;
        }

        /// <summary>在当前职责池中按权重选择一个条目。</summary>
        /// <param name="state">当前职责状态。</param>
        /// <param name="forceUnowned">是否只允许未拥有物品。</param>
        /// <returns>选中的条目，没有可用条目时返回 null。</returns>
        private SupplyItemEntry PickEntry(PoolState state, bool forceUnowned)
        {
            var candidates = new List<SupplyItemEntry>();
            var weights = new List<int>();
            bool hasUnowned = false;
            for (int index = 0; index < _pool.Entries.Count; index++)
            {
                SupplyItemEntry entry = _pool.Entries[index];
                if (!entry.Supports(_selectedRole) || entry.Weight <= 0)
                {
                    continue;
                }

                bool isOwned = state.OwnedItemIds.Contains(entry.ItemId);
                if (!isOwned)
                {
                    hasUnowned = true;
                }

                if (forceUnowned && isOwned)
                {
                    continue;
                }

                candidates.Add(entry);
                weights.Add(GetEffectiveWeight(entry.Weight, isOwned, hasUnowned));
            }

            if (candidates.Count == 0)
            {
                return _pool.TryDraw(_random, _selectedRole, out SupplyItemEntry fallback)
                    ? fallback
                    : null;
            }

            if (hasUnowned && !forceUnowned)
            {
                for (int index = 0; index < candidates.Count; index++)
                {
                    SupplyItemEntry entry = candidates[index];
                    weights[index] = state.OwnedItemIds.Contains(entry.ItemId)
                        ? entry.Weight
                        : GetEffectiveWeight(entry.Weight, false, true);
                }
            }

            return DrawWeighted(candidates, weights);
        }

        /// <summary>计算未拥有物品的四倍权重。</summary>
        /// <param name="weight">原始权重。</param>
        /// <param name="isOwned">物品是否已拥有。</param>
        /// <param name="hasUnowned">池中是否仍有未拥有物品。</param>
        /// <returns>有效抽取权重。</returns>
        private static int GetEffectiveWeight(int weight, bool isOwned, bool hasUnowned)
        {
            if (isOwned || !hasUnowned || weight > int.MaxValue / 4)
            {
                return weight;
            }

            return weight * 4;
        }

        /// <summary>按传入权重从候选列表中抽取一个条目。</summary>
        /// <param name="candidates">候选条目。</param>
        /// <param name="weights">与候选一一对应的权重。</param>
        /// <returns>抽中的条目。</returns>
        private SupplyItemEntry DrawWeighted(
            IReadOnlyList<SupplyItemEntry> candidates,
            IReadOnlyList<int> weights)
        {
            int totalWeight = 0;
            for (int index = 0; index < weights.Count; index++)
            {
                totalWeight = AddWeight(totalWeight, weights[index]);
            }

            int roll = _random.NextInt(totalWeight);
            for (int index = 0; index < candidates.Count; index++)
            {
                if (roll < weights[index])
                {
                    return candidates[index];
                }

                roll -= weights[index];
            }

            return candidates[candidates.Count - 1];
        }

        /// <summary>安全累加职责池权重。</summary>
        /// <param name="current">当前总权重。</param>
        /// <param name="weight">待加入权重。</param>
        /// <returns>限制在整数范围内的总权重。</returns>
        private static int AddWeight(int current, int weight)
        {
            return weight > int.MaxValue - current ? int.MaxValue : current + weight;
        }

        /// <summary>创建带有独立槽位身份的候选。</summary>
        /// <param name="entry">物品池条目。</param>
        /// <returns>新的候选槽位。</returns>
        private MobiusCandidate CreateCandidate(SupplyItemEntry entry)
        {
            _candidateSequence++;
            return new MobiusCandidate(
                $"candidate-{_candidateSequence}",
                entry.ItemId,
                entry.Weight);
        }

        /// <summary>按槽位身份查找候选。</summary>
        /// <param name="state">当前职责状态。</param>
        /// <param name="candidateId">候选槽位身份。</param>
        /// <returns>候选不存在时返回 null。</returns>
        private static MobiusCandidate FindCandidate(PoolState state, string candidateId)
        {
            for (int index = 0; index < state.Candidates.Count; index++)
            {
                if (state.Candidates[index].CandidateId == candidateId)
                {
                    return state.Candidates[index];
                }
            }

            return null;
        }

        /// <summary>保存一个职责的候选、拥有集合和重复计数。</summary>
        private sealed class PoolState
        {
            private readonly List<MobiusCandidate> _candidates = new();

            /// <summary>当前职责的候选槽位。</summary>
            public IReadOnlyList<MobiusCandidate> Candidates => _candidates;

            /// <summary>只读候选视图。</summary>
            public IReadOnlyList<MobiusCandidate> ReadOnlyCandidates { get; private set; } =
                new List<MobiusCandidate>().AsReadOnly();

            /// <summary>已经抓取过的物品标识。</summary>
            public HashSet<string> OwnedItemIds { get; } = new();

            /// <summary>连续相同抓取次数。</summary>
            public int RepeatCount { get; private set; }

            private string _lastItemId;

            /// <summary>加入新的候选并刷新只读视图。</summary>
            /// <param name="candidate">待加入候选。</param>
            public void AddCandidate(MobiusCandidate candidate)
            {
                _candidates.Add(candidate);
                ReadOnlyCandidates = _candidates.AsReadOnly();
            }

            /// <summary>移除已经抓取的候选并刷新只读视图。</summary>
            /// <param name="candidate">待移除候选。</param>
            public void RemoveCandidate(MobiusCandidate candidate)
            {
                _candidates.Remove(candidate);
                ReadOnlyCandidates = _candidates.AsReadOnly();
            }

            /// <summary>记录本次抓取的连续重复次数。</summary>
            /// <param name="itemId">被抓取的物品标识。</param>
            public void RegisterClaim(string itemId, bool wasOwned)
            {
                if (!wasOwned)
                {
                    RepeatCount = 0;
                }
                else
                {
                    RepeatCount = itemId == _lastItemId ? RepeatCount + 1 : 1;
                }

                _lastItemId = itemId;
            }
        }
    }
}
