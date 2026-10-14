using System.Collections.Generic;

namespace EnemySystem.GOAP
{
    /// <summary>用有界最小代价搜索为离散世界状态生成动作序列。</summary>
    public static class GoapPlanner
    {
        private const int MaximumExpandedStates = 128;

        private readonly struct SearchNode
        {
            public SearchNode(GoapFact state, float cost)
            {
                State = state;
                Cost = cost;
            }

            public GoapFact State { get; }
            public float Cost { get; }
        }

        /// <summary>查找满足目标事实的最低代价动作序列。</summary>
        /// <param name="initialState">当前观测到的世界状态。</param>
        /// <param name="goalState">计划需要达到的事实集合。</param>
        /// <param name="actions">本敌人允许使用的动作定义。</param>
        /// <param name="plan">输出的有序动作序列；无计划时为空。</param>
        /// <returns>初始状态已满足目标或找到计划时返回 true。</returns>
        public static bool TryPlan(
            GoapFact initialState,
            GoapFact goalState,
            IReadOnlyList<GoapActionDefinition> actions,
            List<GoapActionDefinition> plan)
        {
            plan.Clear();
            if (Satisfies(initialState, goalState))
            {
                return true;
            }

            List<SearchNode> open = new()
            {
                new SearchNode(initialState, 0f)
            };
            Dictionary<GoapFact, float> bestCosts = new()
            {
                [initialState] = 0f
            };
            Dictionary<GoapFact, (GoapFact Previous, GoapActionDefinition Action)> previous = new();
            HashSet<GoapFact> expanded = new();
            int expandedCount = 0;

            while (open.Count > 0 && expandedCount < MaximumExpandedStates)
            {
                SearchNode current = TakeLowestCost(open);
                if (!expanded.Add(current.State))
                {
                    continue;
                }

                expandedCount++;
                if (Satisfies(current.State, goalState))
                {
                    BuildPlan(current.State, initialState, previous, plan);
                    return true;
                }

                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    GoapActionDefinition action = actions[actionIndex];
                    if (!Satisfies(current.State, action.Preconditions))
                    {
                        continue;
                    }

                    GoapFact nextState = (current.State | action.AddedFacts) & ~action.RemovedFacts;
                    if (nextState == current.State)
                    {
                        continue;
                    }

                    float nextCost = current.Cost + action.Cost;
                    if (bestCosts.TryGetValue(nextState, out float knownCost) && knownCost <= nextCost)
                    {
                        continue;
                    }

                    bestCosts[nextState] = nextCost;
                    previous[nextState] = (current.State, action);
                    open.Add(new SearchNode(nextState, nextCost));
                }
            }

            return false;
        }

        /// <summary>检查状态是否包含指定事实集合。</summary>
        /// <param name="state">当前事实。</param>
        /// <param name="facts">所需事实集合。</param>
        /// <returns>当前状态包含全部所需事实时返回 true。</returns>
        private static bool Satisfies(GoapFact state, GoapFact facts)
        {
            return (state & facts) == facts;
        }

        /// <summary>从未处理状态中取出代价最小的节点。</summary>
        /// <param name="open">待展开节点列表。</param>
        /// <returns>总代价最低的节点。</returns>
        private static SearchNode TakeLowestCost(List<SearchNode> open)
        {
            int lowestIndex = 0;
            for (int index = 1; index < open.Count; index++)
            {
                if (open[index].Cost < open[lowestIndex].Cost)
                {
                    lowestIndex = index;
                }
            }

            SearchNode lowest = open[lowestIndex];
            open.RemoveAt(lowestIndex);
            return lowest;
        }

        /// <summary>沿父节点记录回溯并反转动作序列。</summary>
        /// <param name="state">满足目标的终点状态。</param>
        /// <param name="initialState">计划起始状态。</param>
        /// <param name="previous">搜索过程中记录的父状态与动作。</param>
        /// <param name="plan">接收有序动作序列的输出列表。</param>
        private static void BuildPlan(
            GoapFact state,
            GoapFact initialState,
            Dictionary<GoapFact, (GoapFact Previous, GoapActionDefinition Action)> previous,
            List<GoapActionDefinition> plan)
        {
            while (state != initialState && previous.TryGetValue(state, out var step))
            {
                plan.Add(step.Action);
                state = step.Previous;
            }

            plan.Reverse();
        }
    }
}
