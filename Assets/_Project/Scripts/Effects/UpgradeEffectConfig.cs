using UnityEngine;

namespace EffectSystem
{
    /// <summary>升级表现的静态配置。</summary>
    [CreateAssetMenu(fileName = "UpgradeEffectConfig", menuName = "Effects/Upgrade Effect")]
    public class UpgradeEffectConfig : ScriptableObject
    {
        [Header("特效预制体")]
        public GameObject effectPrefab;
        public GameObject successEffectPrefab;
        public GameObject failedEffectPrefab;

        [Header("特效设置")]
        [Min(0.01f)] public float duration = 2f;
        public Vector3 effectScale = Vector3.one;
        public Vector3 effectOffset = Vector3.zero;
        public bool loopEffect;

        [Header("声音设置")]
        public AudioClip successSound;
        public AudioClip failedSound;
        [Range(0f, 1f)] public float soundVolume = 1f;

        [Header("视觉反馈")]
        public bool enableGlow = true;
        public Color glowColor = Color.cyan;
        [Min(0f)] public float glowDuration = 1f;
    }
}
