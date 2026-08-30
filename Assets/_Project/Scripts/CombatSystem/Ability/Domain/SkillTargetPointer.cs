using System;
using Interfaces;

namespace CombatSystem
{
    /// <summary>所有卡牌共用的目标确认状态；表现层只负责驱动 Confirm。</summary>
    public sealed class SkillTargetPointer
    {
        public IDamageable CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget.IsAliveAndValid();
        public event Action<IDamageable> TargetChanged;

        public bool Confirm(IDamageable target)
        {
            if (!target.IsAliveAndValid())
                return false;

            CurrentTarget = target;
            TargetChanged?.Invoke(target);
            return true;
        }

        public void Clear()
        {
            if (CurrentTarget == null)
                return;
            CurrentTarget = null;
            TargetChanged?.Invoke(null);
        }
    }
}
