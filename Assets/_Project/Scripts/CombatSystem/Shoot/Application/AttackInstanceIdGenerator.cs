using System.Threading;

namespace CombatSystem
{
    /// <summary>
    /// 线程安全地分配当前战斗容器内的攻击实例标识。
    /// </summary>
    public sealed class AttackInstanceIdGenerator : IAttackInstanceIdGenerator
    {
        private int _nextId;

        /// <summary>分配下一个大于零的攻击实例标识。</summary>
        /// <returns>新的攻击实例标识。</returns>
        public int Next()
        {
            int id = Interlocked.Increment(ref _nextId);
            return id > 0 ? id : Interlocked.Increment(ref _nextId);
        }
    }
}
