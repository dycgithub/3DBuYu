using UnityEngine;

namespace CombatSystem
{
    /// <summary>Buff 的运行时基类，保存来源、层数和剩余时间。</summary>
    public abstract class BuffBase
    {
        public BuffConfig Config { get; set; }
        public float TimeRemaining { get; set; }
        public int SourceId { get; set; }
        public int Stacks { get; set; } = 1;

        /// <summary>
        /// 是否到期。Duration &lt;= 0 表示常驻 buff（如弹药加成），不会因计时到期被移除。
        /// </summary>
        public bool IsExpired => Config != null && Config.Duration > 0f && TimeRemaining <= 0f;

        public virtual void OnApply() { }
        public virtual void OnExpire() { }
        public virtual void OnTick(float dt) { }
    }
}
