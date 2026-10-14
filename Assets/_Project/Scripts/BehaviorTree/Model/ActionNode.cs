using System;

namespace EnemySystem.BehaviorTree
{
    /// <summary>把一次行为执行和取消回调封装为叶节点。</summary>
    public sealed class ActionNode : BehaviorNode
    {
        private readonly Func<BehaviorStatus> _tick;
        private readonly Action _reset;

        /// <summary>创建无额外清理动作的行为节点。</summary>
        /// <param name="tick">推进行为并返回节点状态。</param>
        public ActionNode(Func<BehaviorStatus> tick)
            : this(tick, null)
        {
        }

        /// <summary>创建行为节点。</summary>
        /// <param name="tick">推进行为并返回节点状态。</param>
        /// <param name="reset">行为被取消或结束时执行的清理。</param>
        public ActionNode(Func<BehaviorStatus> tick, Action reset = null)
        {
            _tick = tick;
            _reset = reset;
        }

        /// <summary>执行一次行为委托。</summary>
        /// <returns>行为委托返回的状态。</returns>
        public override BehaviorStatus Tick()
        {
            return _tick();
        }

        /// <summary>清理行为节点拥有的计时与移动命令。</summary>
        public override void Reset()
        {
            _reset?.Invoke();
        }
    }
}
