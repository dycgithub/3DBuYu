using UnityEngine;

namespace CombatSystem
{
    /// <summary>Buff 的静态资产配置，不保存运行时层数和剩余时间。</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Buff Config")]
    public class BuffConfig : ScriptableObject
    {
        public BuffType Type;
        public float Duration;
        public float Value;
        public BuffStackPolicy StackPolicy = BuffStackPolicy.RefreshDuration;
        [Min(1)] public int MaxStacks = 1;
        public GameObject VisualEffect;
    }
}
