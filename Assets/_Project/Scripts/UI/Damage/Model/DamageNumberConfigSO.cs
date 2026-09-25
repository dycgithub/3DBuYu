using UnityEngine;

namespace _Project.UI.Damage
{
    /// <summary>伤害跳字池、聚合和屏幕动画参数。</summary>
    [CreateAssetMenu(fileName = "DamageNumberConfig", menuName = "UI/Damage Number Config")]
    public sealed class DamageNumberConfigSO : ScriptableObject
    {
        [Header("对象池")]
        [Min(0)] public int initialPoolCapacity = 32;
        [Min(1)] public int maximumRetained = 300;
        [Min(1)] public int maximumVisible = 150;

        [Header("聚合")]
        [Min(0f)] public float aggregationWindow = 0.12f;
        [Min(0f)] public float duration = 0.75f;

        [Header("裁剪")]
        [Min(0f)] public float maximumDistance = 80f;
        [Min(0f)] public float horizontalOffset = 48f;
        [Min(0f)] public float verticalOffset = 24f;
        [Min(0f)] public float verticalDrift = 36f;
    }
}
