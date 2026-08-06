using UnityEngine;

namespace ShootingSystem.Buffs
{
    public abstract class BuffBase
    {
        public BuffConfig Config { get; set; }
        public float TimeRemaining { get; set; }

        /// <summary>
        /// 是否到期。Duration &lt;= 0 表示常驻 buff（如弹药加成），不会因计时到期被移除。
        /// </summary>
        public bool IsExpired => Config != null && Config.Duration > 0f && TimeRemaining <= 0f;

        public virtual void OnApply() { }
        public virtual void OnExpire() { }
        public virtual void OnTick(float dt) { }
    }
}
