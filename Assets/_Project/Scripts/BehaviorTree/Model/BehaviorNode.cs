namespace EnemySystem.BehaviorTree
{
    /// <summary>行为树节点的执行与重置契约。</summary>
    public abstract class BehaviorNode
    {
        /// <summary>推进节点一次。</summary>
        /// <returns>本次执行结果。</returns>
        public abstract BehaviorStatus Tick();

        /// <summary>取消当前执行并清空节点持有状态。</summary>
        public virtual void Reset()
        {
        }
    }
}
