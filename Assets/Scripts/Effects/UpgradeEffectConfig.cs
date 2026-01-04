using UnityEngine;

namespace EffectSystem
{
    /// <summary>
    /// 升级特效配置，通过ScriptableObject存储
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeEffectConfig", menuName = "Effects/Upgrade Effect")]
    public class UpgradeEffectConfig : ScriptableObject
    {
        [Header("特效预制体")]
        [Tooltip("升级特效预制体")]
        public GameObject effectPrefab;

        [Tooltip("升级成功特效预制体")]
        public GameObject successEffectPrefab;

        [Tooltip("升级失败特效预制体")]
        public GameObject failedEffectPrefab;

        [Header("特效设置")]
        [Tooltip("特效持续时间(秒)")]
        public float duration = 2f;

        [Tooltip("特效缩放")]
        public Vector3 effectScale = Vector3.one;

        [Tooltip("特效位置偏移")]
        public Vector3 effectOffset = Vector3.zero;

        [Tooltip("特效持续循环播放")]
        public bool loopEffect = false;

        [Header("声音设置")]
        [Tooltip("升级成功音效")]
        public AudioClip successSound;

        [Tooltip("升级失败音效")]
        public AudioClip failedSound;

        [Tooltip("音效音量")]
        [Range(0f, 1f)]
        public float soundVolume = 1f;

        [Header("视觉反馈")]
        [Tooltip("是否发光")]
        public bool enableGlow = true;

        [Tooltip("发光颜色")]
        public Color glowColor = Color.cyan;

        [Tooltip("发光持续时间")]
        public float glowDuration = 1f;

        /// <summary>
        /// 播放升级成功特效
        /// </summary>
        public void PlaySuccessEffect(Vector3 position)
        {
            if (successEffectPrefab != null)
            {
                SpawnEffect(successEffectPrefab, position);
            }
            else if (effectPrefab != null)
            {
                SpawnEffect(effectPrefab, position);
            }

            if (successSound != null)
            {
                AudioSource.PlayClipAtPoint(successSound, position, soundVolume);
            }
        }

        /// <summary>
        /// 播放升级失败特效
        /// </summary>
        public void PlayFailedEffect(Vector3 position)
        {
            if (failedEffectPrefab != null)
            {
                SpawnEffect(failedEffectPrefab, position);
            }

            if (failedSound != null)
            {
                AudioSource.PlayClipAtPoint(failedSound, position, soundVolume);
            }
        }

        /// <summary>
        /// 播放升级特效(通用)
        /// </summary>
        public void PlayEffect(Vector3 position)
        {
            if (effectPrefab != null)
            {
                SpawnEffect(effectPrefab, position);
            }
        }

        private void SpawnEffect(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;

            GameObject effect = Instantiate(prefab, position + effectOffset, Quaternion.identity);
            effect.transform.localScale = effectScale;

            // 自动销毁
            if (!loopEffect)
            {
                var destroyComponent = effect.AddComponent<AutoDestroy>();
                destroyComponent.lifetime = duration;
            }
        }
    }

    /// <summary>
    /// 自动销毁组件
    /// </summary>
    public class AutoDestroy : MonoBehaviour
    {
        public float lifetime = 2f;

        void Start()
        {
            Destroy(gameObject, lifetime);
        }
    }
}
