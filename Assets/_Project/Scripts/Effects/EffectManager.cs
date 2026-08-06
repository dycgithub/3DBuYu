using UnityEngine;
using System.Collections.Generic;
using Services;

namespace EffectSystem
{
    public class EffectManager : MonoBehaviour, IEffectService
    {
        private Dictionary<string, GameObject> effectPrefabs = new Dictionary<string, GameObject>();

        void Awake()
        {
        }

        public void RegisterEffect(string name, GameObject prefab)
        {
            if (!effectPrefabs.ContainsKey(name))
            {
                effectPrefabs[name] = prefab;
            }
        }

        public GameObject PlayEffect(string name, Vector3 position)
        {
            if (effectPrefabs.TryGetValue(name, out GameObject prefab) && prefab != null)
            {
                return Instantiate(prefab, position, Quaternion.identity);
            }

            Debug.LogWarning($"Effect '{name}' not found!");
            return null;
        }

        public GameObject PlayEffect(string name, Transform parent)
        {
            if (effectPrefabs.TryGetValue(name, out GameObject prefab) && prefab != null)
            {
                return Instantiate(prefab, parent);
            }

            Debug.LogWarning($"Effect '{name}' not found!");
            return null;
        }

        void IEffectService.Play(string effectName, Vector3 position)
        {
            PlayEffect(effectName, position);
        }

        void IEffectService.Stop(string effectName)
        {
            var instances = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in instances)
            {
                if (go != null && go.name.StartsWith(effectName))
                {
                    var ps = go.GetComponent<ParticleSystem>();
                    if (ps != null) ps.Stop();
                }
            }
        }
    }
}
