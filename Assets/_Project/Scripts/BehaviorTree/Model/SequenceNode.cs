using System;

namespace EnemySystem.BehaviorTree
{
    /// <summary>按顺序推进子节点，失败时重置本序列。</summary>
    public sealed class SequenceNode : BehaviorNode
    {
        private readonly BehaviorNode[] _children;
        private int _activeChild;

        /// <summary>创建顺序组合节点。</summary>
        /// <param name="children">按执行顺序排列的子节点。</param>
        public SequenceNode(params BehaviorNode[] children)
        {
            _children = children ?? Array.Empty<BehaviorNode>();
        }

        /// <summary>从当前运行节点继续，直到失败、运行中或全部成功。</summary>
        /// <returns>序列的执行结果。</returns>
        public override BehaviorStatus Tick()
        {
            while (_activeChild < _children.Length)
            {
                BehaviorStatus status = _children[_activeChild].Tick();
                if (status == BehaviorStatus.Running)
                {
                    return status;
                }

                if (status == BehaviorStatus.Failure)
                {
                    Reset();
                    return status;
                }

                _activeChild++;
            }

            Reset();
            return BehaviorStatus.Success;
        }

        /// <summary>重置序列及全部子节点。</summary>
        public override void Reset()
        {
            for (int index = 0; index < _children.Length; index++)
            {
                _children[index].Reset();
            }

            _activeChild = 0;
        }
    }
}
