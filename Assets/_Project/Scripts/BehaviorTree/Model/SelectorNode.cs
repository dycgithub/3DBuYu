using System;

namespace EnemySystem.BehaviorTree
{
    /// <summary>按优先级尝试子节点，并中止被更高优先级节点抢占的行为。</summary>
    public sealed class SelectorNode : BehaviorNode
    {
        private readonly BehaviorNode[] _children;
        private int _runningChild = -1;

        /// <summary>创建优先级选择节点。</summary>
        /// <param name="children">按优先级排列的子节点。</param>
        public SelectorNode(params BehaviorNode[] children)
        {
            _children = children ?? Array.Empty<BehaviorNode>();
        }

        /// <summary>从最高优先级开始检查子节点。</summary>
        /// <returns>第一个不失败子节点的状态，全部失败时返回 Failure。</returns>
        public override BehaviorStatus Tick()
        {
            int previousRunningChild = _runningChild;
            for (int index = 0; index < _children.Length; index++)
            {
                BehaviorStatus status = _children[index].Tick();
                if (status == BehaviorStatus.Failure)
                {
                    continue;
                }

                if (previousRunningChild >= 0 && previousRunningChild != index)
                {
                    _children[previousRunningChild].Reset();
                }

                _runningChild = status == BehaviorStatus.Running ? index : -1;
                if (status == BehaviorStatus.Success)
                {
                    _children[index].Reset();
                }

                return status;
            }

            if (previousRunningChild >= 0)
            {
                _children[previousRunningChild].Reset();
            }

            _runningChild = -1;
            return BehaviorStatus.Failure;
        }

        /// <summary>重置选择节点及所有子节点。</summary>
        public override void Reset()
        {
            for (int index = 0; index < _children.Length; index++)
            {
                _children[index].Reset();
            }

            _runningChild = -1;
        }
    }
}
