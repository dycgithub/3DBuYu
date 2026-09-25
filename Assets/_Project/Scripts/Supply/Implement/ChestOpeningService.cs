using System;
using System.Collections.Generic;
using Services;

namespace Supply
{
    /// <summary>执行宝箱开箱、方块事件和奖励结算。</summary>
    public sealed class ChestOpeningService
    {
        private const int InitialColorLevel = 10;
        private const int MaximumClickCount = 10;
        private const int FirstCheckClickCount = 5;
        private const int EventCount = 8;
        private const int NoChangeEvent = 0;
        private const int DirectSettlementEvent = 1;
        private const int SingleBreakEvent = 2;
        private const int NeighborBreakEvent = 3;
        private const int SingleColorEvent = 4;
        private const int NeighborColorEvent = 5;
        private const int AllBreakEvent = 6;
        private const int AllColorEvent = 7;

        private readonly IPointsService _points;
        private readonly SupplyItemPool _pool;
        private readonly IRandomSource _random;
        private readonly ChestRules _rules;
        private readonly List<ChestCubeState> _cubes = new();
        private readonly IReadOnlyList<ChestCubeState> _readOnlyCubes;
        private bool _isOpen;
        private bool _isSettled;
        private int _clickCount;
        private bool _isBusy;

        /// <summary>当前宝箱方块状态。</summary>
        public IReadOnlyList<ChestCubeState> Cubes => _readOnlyCubes;

        /// <summary>当前所有未碎裂方块的颜色分数之和。</summary>
        public int CurrentScore => GetScore();

        /// <summary>创建宝箱服务。</summary>
        /// <param name="points">积分服务。</param>
        /// <param name="pool">宝箱奖励物品池。</param>
        /// <param name="random">可重复随机源。</param>
        /// <param name="rules">宝箱规则。</param>
        public ChestOpeningService(
            IPointsService points,
            SupplyItemPool pool,
            IRandomSource random,
            ChestRules rules)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _readOnlyCubes = _cubes.AsReadOnly();
        }

        /// <summary>支付开箱费用并创建八个初始黑色方块。</summary>
        /// <returns>开箱成功时返回 true。</returns>
        public bool TryOpen()
        {
            if (_isBusy || _isOpen || !_pool.HasEntries(SupplyItemRole.All) ||
                !_points.HasEnoughPoints(_rules.OpeningCost))
            {
                return false;
            }

            _isBusy = true;
            try
            {
                if (!_points.SpendPoints(_rules.OpeningCost, "Supply 宝箱"))
                {
                    return false;
                }

                _cubes.Clear();
                for (int index = 0; index < CubeTopology.CubeCount; index++)
                {
                    _cubes.Add(new ChestCubeState(index, InitialColorLevel));
                }

                _clickCount = 0;
                _isOpen = true;
                _isSettled = false;
                return true;
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>执行一次点击事件，并在满足规则时返回结算。</summary>
        /// <param name="cubeIndex">点击的方块索引；点击空处可传入无效索引。</param>
        /// <returns>本次点击结果。</returns>
        public ChestActionResult AdvanceClick(int cubeIndex)
        {
            if (_isBusy || !_isOpen || _isSettled)
            {
                return new ChestActionResult(false, false, null);
            }

            _isBusy = true;
            try
            {
                _clickCount++;
                int targetIndex = ResolveTarget(cubeIndex);
                int eventCode = SelectEvent();
                if (eventCode == DirectSettlementEvent)
                {
                    return SettleAction();
                }

                ApplyEvent(eventCode, targetIndex);
                if (_isSettled)
                {
                    return SettleAction();
                }

                if (_clickCount == FirstCheckClickCount && ShouldSettleEarly())
                {
                    return SettleAction();
                }

                if (_clickCount >= MaximumClickCount)
                {
                    return SettleAction();
                }

                return new ChestActionResult(true, false, null);
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>按当前点击阶段选择可执行的宝箱事件。</summary>
        /// <returns>当前阶段允许执行的事件编码。</returns>
        private int SelectEvent()
        {
            int eventCode = _random.NextInt(EventCount);
            if (_clickCount <= FirstCheckClickCount &&
                (eventCode == DirectSettlementEvent || eventCode == AllBreakEvent))
            {
                return NoChangeEvent;
            }

            return eventCode;
        }

        /// <summary>判断第五次事件后的提前结算概率。</summary>
        /// <returns>满足提前结算条件时返回 true。</returns>
        private bool ShouldSettleEarly()
        {
            if (_rules.SettleChance >= 1f)
            {
                return true;
            }

            return _rules.SettleChance > 0f && _random.NextFloat() < _rules.SettleChance;
        }

        /// <summary>根据点击索引或未碎裂方块随机确定事件中心。</summary>
        /// <param name="cubeIndex">点击的方块索引。</param>
        /// <returns>事件中心索引。</returns>
        private int ResolveTarget(int cubeIndex)
        {
            if (IsAvailable(cubeIndex))
            {
                return cubeIndex;
            }

            var available = new List<int>();
            for (int index = 0; index < _cubes.Count; index++)
            {
                if (!_cubes[index].IsBroken)
                {
                    available.Add(index);
                }
            }

            if (available.Count == 0)
            {
                return 0;
            }

            return available[_random.NextInt(available.Count)];
        }

        /// <summary>执行一个随机事件。</summary>
        /// <param name="eventCode">事件编码。</param>
        /// <param name="targetIndex">事件中心。</param>
        private void ApplyEvent(int eventCode, int targetIndex)
        {
            switch (eventCode)
            {
                case NoChangeEvent:
                case DirectSettlementEvent:
                    return;
                case SingleBreakEvent:
                    BreakIndexes(new[] { targetIndex });
                    return;
                case NeighborBreakEvent:
                    var neighbors = new List<int> { targetIndex };
                    foreach (int neighbor in CubeTopology.GetNeighbors(targetIndex))
                    {
                        neighbors.Add(neighbor);
                    }

                    BreakIndexes(neighbors);
                    return;
                case SingleColorEvent:
                    ChangeColor(targetIndex);
                    return;
                case NeighborColorEvent:
                    ChangeNeighborColor(targetIndex);
                    return;
                case AllBreakEvent:
                    var allIndexes = new List<int>();
                    for (int index = 0; index < _cubes.Count; index++)
                    {
                        allIndexes.Add(index);
                    }

                    BreakIndexes(allIndexes);
                    return;
                case AllColorEvent:
                    ChangeAllColor();
                    return;
                default:
                    return;
            }
        }

        /// <summary>按前五次事件的最小保留规则碎裂指定方块。</summary>
        /// <param name="indexes">待碎裂方块索引。</param>
        private void BreakIndexes(IReadOnlyList<int> indexes)
        {
            int remaining = CountUnbroken();
            for (int index = 0; index < indexes.Count; index++)
            {
                ChestCubeState cube = _cubes[indexes[index]];
                if (cube.IsBroken)
                {
                    continue;
                }

                if (_clickCount <= FirstCheckClickCount &&
                    remaining <= _rules.MinimumRemainingCubes)
                {
                    break;
                }

                cube.Break();
                remaining--;
            }

            if (remaining == 0)
            {
                _isSettled = true;
            }
        }

        /// <summary>将单个方块变更为不同的随机颜色等级。</summary>
        /// <param name="index">目标方块索引。</param>
        private void ChangeColor(int index)
        {
            ChestCubeState cube = _cubes[index];
            if (cube.IsBroken)
            {
                return;
            }

            cube.SetColor(NextDifferentColor(cube.ColorLevel));
        }

        /// <summary>将目标方块及其共享面邻居变更为不同的随机颜色。</summary>
        /// <param name="index">事件中心索引。</param>
        private void ChangeNeighborColor(int index)
        {
            int newColor = NextDifferentColor(_cubes[index].ColorLevel);
            _cubes[index].SetColor(newColor);
            foreach (int neighbor in CubeTopology.GetNeighbors(index))
            {
                if (!_cubes[neighbor].IsBroken)
                {
                    _cubes[neighbor].SetColor(newColor);
                }
            }
        }

        /// <summary>将所有未碎裂方块变更为同一个新颜色。</summary>
        private void ChangeAllColor()
        {
            int currentColor = 0;
            for (int index = 0; index < _cubes.Count; index++)
            {
                if (!_cubes[index].IsBroken)
                {
                    currentColor = _cubes[index].ColorLevel;
                    break;
                }
            }

            int newColor = NextDifferentColor(currentColor);
            for (int index = 0; index < _cubes.Count; index++)
            {
                if (!_cubes[index].IsBroken)
                {
                    _cubes[index].SetColor(newColor);
                }
            }
        }

        /// <summary>生成一个与当前等级不同的颜色等级。</summary>
        /// <param name="currentColor">当前等级。</param>
        /// <returns>不同的颜色等级。</returns>
        private int NextDifferentColor(int currentColor)
        {
            int color = _random.NextInt(10) + 1;
            return color == currentColor ? color % 10 + 1 : color;
        }

        /// <summary>创建结算动作并锁定宝箱。</summary>
        /// <returns>已结算动作结果。</returns>
        private ChestActionResult SettleAction()
        {
            _isSettled = true;
            _isOpen = false;
            return new ChestActionResult(true, true, CreateSettlement());
        }

        /// <summary>按未碎裂方块生成统一强度的奖励。</summary>
        /// <returns>宝箱结算数据。</returns>
        private ChestSettlement CreateSettlement()
        {
            int itemCount = CountUnbroken();
            int score = GetScore();
            PowerLevelValue power = ItemPowerTable.Resolve(score);
            var rewards = new List<SupplyRewardItem>();

            for (int index = 0; index < itemCount; index++)
            {
                if (_pool.TryDraw(_random, SupplyItemRole.All, out SupplyItemEntry entry))
                {
                    rewards.Add(new SupplyRewardItem(entry.ItemId, power.Level));
                }
            }

            return new ChestSettlement(itemCount, score, power.Level, rewards.AsReadOnly());
        }

        /// <summary>计算所有未碎裂方块的颜色分数。</summary>
        /// <returns>当前强度分数。</returns>
        private int GetScore()
        {
            int score = 0;
            for (int index = 0; index < _cubes.Count; index++)
            {
                ChestCubeState cube = _cubes[index];
                if (!cube.IsBroken)
                {
                    score += ColorScore(cube.ColorLevel);
                }
            }

            return score;
        }

        /// <summary>统计当前未碎裂方块数量。</summary>
        /// <returns>未碎裂方块数量。</returns>
        private int CountUnbroken()
        {
            int count = 0;
            for (int index = 0; index < _cubes.Count; index++)
            {
                if (!_cubes[index].IsBroken)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>判断指定索引是否指向未碎裂方块。</summary>
        /// <param name="index">方块索引。</param>
        /// <returns>索引有效且方块未碎裂时返回 true。</returns>
        private bool IsAvailable(int index)
        {
            return index >= 0 && index < _cubes.Count && !_cubes[index].IsBroken;
        }

        /// <summary>读取颜色等级对应的宝箱强度分数。</summary>
        /// <param name="colorLevel">颜色等级。</param>
        /// <returns>颜色分数。</returns>
        private static int ColorScore(int colorLevel)
        {
            switch (colorLevel)
            {
                case 1: return 3;
                case 2: return 5;
                case 3: return 7;
                case 4: return 8;
                case 5: return 9;
                case 6: return 11;
                case 7: return 12;
                case 8: return 13;
                case 9: return 15;
                default: return 16;
            }
        }
    }
}
