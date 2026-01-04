using UnityEngine;
using System.Collections.Generic;

namespace EffectSystem
{
    /// <summary>
    /// 特效管理器
    /// </summary>
    public class EffectManager : MonoBehaviour
    {
        public static EffectManager Instance { get; private set; }

        private Dictionary<string, GameObject> effectPrefabs = new Dictionary<string, GameObject>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 注册特效预制体
        /// </summary>
        public void RegisterEffect(string name, GameObject prefab)
        {
            if (!effectPrefabs.ContainsKey(name))
            {
                effectPrefabs[name] = prefab;
            }
        }

        /// <summary>
        /// 播放特效
        /// </summary>
        public GameObject PlayEffect(string name, Vector3 position)
        {
            if (effectPrefabs.TryGetValue(name, out GameObject prefab) && prefab != null)
            {
                return Instantiate(prefab, position, Quaternion.identity);
            }

            Debug.LogWarning($"Effect '{name}' not found!");
            return null;
        }

        /// <summary>
        /// 播放带父物体的特效
        /// </summary>
        public GameObject PlayEffect(string name, Transform parent)
        {
            if (effectPrefabs.TryGetValue(name, out GameObject prefab) && prefab != null)
            {
                return Instantiate(prefab, parent);
            }

            Debug.LogWarning($"Effect '{name}' not found!");
            return null;
        }
    }
}
