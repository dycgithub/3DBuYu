namespace EnemySystem.BehaviorTree
{
    /// <summary>持有一棵独立于 ScriptableObject 的行为树实例。</summary>
    public sealed class BehaviorTree
    {
        private readonly BehaviorNode _root;

        /// <summary>绑定行为树根节点。</summary>
        /// <param name="root">要推进的根节点。</param>
        public BehaviorTree(BehaviorNode root)
        {
            _root = root;
        }

        /// <summary>推进根节点一次。</summary>
        /// <returns>整棵树的执行状态。</returns>
        public BehaviorStatus Tick()
        {
            return _root.Tick();
        }

        /// <summary>重置树内节点，取消未完成动作。</summary>
        public void Reset()
        {
            _root.Reset();
        }
    }
}
