using Interfaces;

namespace Play.TargetMark
{
    /// <summary>向战斗系统提供当前标记队列的活动队首目标。</summary>
    public sealed class MarkedTargetService
    {
        private IDamageable _target;

        /// <summary>当前手动标记目标。</summary>
        public IDamageable Target => _target;

        /// <summary>设置有效目标；重复设置当前目标不会取消标记。</summary>
        /// <param name="target">待标记的可受伤害目标。</param>
        /// <returns>目标发生变化时返回 true。</returns>
        public bool TrySetTarget(IDamageable target)
        {
            if (!target.IsAliveAndValid() || ReferenceEquals(_target, target))
            {
                return false;
            }

            _target = target;
            return true;
        }

        /// <summary>清除当前手动标记目标。</summary>
        public void ClearTarget()
        {
            if (ReferenceEquals(_target, null))
            {
                return;
            }

            _target = null;
        }
    }
}
