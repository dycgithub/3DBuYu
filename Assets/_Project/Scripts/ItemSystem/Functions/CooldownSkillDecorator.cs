using UnityEngine;

namespace ItemSystem.Functions
{
    /// <summary>
    /// 冷却修饰器：限制被包裹技能的激活频率。
    /// 冷却期间调用 Activate 会被静默跳过；IsReady 可查询当前是否可用。
    /// 示例：new CooldownSkillDecorator(new KillAllEnemiesSkill(), 10f)
    /// </summary>
    public class CooldownSkillDecorator : SkillDecorator
    {
        private readonly float _cooldown;
        private float _nextAvailableTime;

        public CooldownSkillDecorator(ISkill inner, float cooldownSeconds) : base(inner)
        {
            _cooldown = Mathf.Max(0f, cooldownSeconds);
        }

        public bool IsReady => Time.time >= _nextAvailableTime;

        /// <summary>下次可用时刻(Time.time 时间轴),供 UI 显示冷却进度。</summary>
        public float NextAvailableTime => _nextAvailableTime;

        public override string Name => $"{Inner.Name}(冷却 {_cooldown:F1}s)";

        protected override void OnActivate(IItemActivationContext context)
        {
            if (!IsReady) return;
            base.OnActivate(context); // BeforeActivate -> Inner.Activate -> AfterActivate
            _nextAvailableTime = Time.time + _cooldown;
        }
    }
}
