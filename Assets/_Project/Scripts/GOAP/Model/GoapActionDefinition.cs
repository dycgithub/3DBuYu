using System;

namespace EnemySystem.GOAP
{
    /// <summary>描述 GOAP 动作的前置事实、结果事实与规划代价。</summary>
    public sealed class GoapActionDefinition
    {
        private const float MinimumActionCost = 0.01f;

        /// <summary>创建不可变动作定义。</summary>
        /// <param name="id">动作标识。</param>
        /// <param name="preconditions">执行前必须成立的事实。</param>
        /// <param name="addedFacts">动作成功后成立的事实。</param>
        /// <param name="removedFacts">动作成功后失效的事实。</param>
        /// <param name="cost">规划代价，必须大于零。</param>
        public GoapActionDefinition(
            GoapActionId id,
            GoapFact preconditions,
            GoapFact addedFacts,
            GoapFact removedFacts,
            float cost)
        {
            Id = id;
            Preconditions = preconditions;
            AddedFacts = addedFacts;
            RemovedFacts = removedFacts;
            Cost = Math.Max(MinimumActionCost, cost);
        }

        /// <summary>动作标识。</summary>
        public GoapActionId Id { get; }

        /// <summary>动作需要成立的事实。</summary>
        public GoapFact Preconditions { get; }

        /// <summary>动作成功后增加的事实。</summary>
        public GoapFact AddedFacts { get; }

        /// <summary>动作成功后移除的事实。</summary>
        public GoapFact RemovedFacts { get; }

        /// <summary>规划代价。</summary>
        public float Cost { get; }
    }
}
