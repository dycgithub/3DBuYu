using System;

namespace EnemySystem.BehaviorTree
{
    /// <summary>重复推进一个行为节点指定次数。</summary>
    public sealed class RepeatNode : BehaviorNode
    {
        private readonly BehaviorNode _child;
        private readonly Func<int> _count;
        private int _completedCount;

        /// <summary>创建重复节点。</summary>
        /// <param name="child">每轮执行的子节点。</param>
        /// <param name="count">本轮总执行次数。</param>
        public RepeatNode(BehaviorNode child, Func<int> count)
        {
            _child = child;
            _count = count;
        }

        /// <summary>推进当前重复轮次。</summary>
        /// <returns>Running、Failure 或所有轮次完成后的 Success。</returns>
        public override BehaviorStatus Tick()
        {
            int targetCount = Math.Max(0, _count());
            while (_completedCount < targetCount)
            {
                BehaviorStatus status = _child.Tick();
                if (status == BehaviorStatus.Running)
                {
                    return status;
                }

                if (status == BehaviorStatus.Failure)
                {
                    Reset();
                    return status;
                }

                _completedCount++;
                _child.Reset();
            }

            Reset();
            return BehaviorStatus.Success;
        }

        /// <summary>清空重复次数和子节点状态。</summary>
        public override void Reset()
        {
            _completedCount = 0;
            _child.Reset();
        }
    }
}
