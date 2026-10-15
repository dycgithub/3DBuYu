using UnityEngine;

namespace EnemySystem.Visual
{
    /// <summary>
    /// 敌人视觉参数资产。
    /// <para>基础颜色来自类型材质；裂纹、发光和反馈时长由 ECS 实例状态使用。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyVisualConfig", menuName = "Enemy/Visual Config")]
    public sealed class EnemyVisualConfigSO : ScriptableObject
    {
        [Header("层级")]
        public EnemyVisualTier tier = EnemyVisualTier.Normal;

        [Header("颜色")]
        public Color crackColor = new(1f, 0.25f, 0.05f, 1f);
        public Color emissionColor = new(1f, 0.05f, 0.01f, 1f);

        [Header("反馈")]
        [Min(0f)] public float damageFlashDuration = 0.08f;
        [Min(0f)] public float deathDuration = 0.18f;
    }
}
