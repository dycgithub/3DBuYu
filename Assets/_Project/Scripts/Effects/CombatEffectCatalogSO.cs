using System;
using System.Collections.Generic;
using UnityEngine;

namespace EffectSystem
{
    /// <summary>单个战斗特效的预制体、生命周期与对象池配置。</summary>
    [Serializable]
    public sealed class EffectCatalogEntry
    {
        public EffectId Id;
        public GameObject Prefab;
        [Min(0f)] public float Lifetime;
        [Min(0)] public int PrewarmCount;
        [Min(1)] public int MaximumRetained = 64;
    }

    /// <summary>集中维护战斗事件到视觉预制体的映射。</summary>
    [CreateAssetMenu(fileName = "CombatEffectCatalog", menuName = "Combat/Effect Catalog")]
    public sealed class CombatEffectCatalogSO : ScriptableObject
    {
        [SerializeField] private List<EffectCatalogEntry> _entries = new();

        public IReadOnlyList<EffectCatalogEntry> Entries => _entries;

        public bool TryGet(EffectId id, out EffectCatalogEntry entry)
        {
            if (id == EffectId.None)
            {
                entry = null;
                return false;
            }

            for (int index = 0; index < _entries.Count; index++)
            {
                EffectCatalogEntry candidate = _entries[index];
                if (candidate != null && candidate.Id == id)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var ids = new HashSet<EffectId>();
            for (int index = 0; index < _entries.Count; index++)
            {
                EffectCatalogEntry entry = _entries[index];
                if (entry == null)
                    continue;

                if (entry.Id == EffectId.None)
                    Debug.LogError($"[CombatEffectCatalogSO] {name} 不能使用 None 作为配置键。", this);
                if (!ids.Add(entry.Id))
                    Debug.LogError($"[CombatEffectCatalogSO] {name} 存在重复配置键: {entry.Id}。", this);
                if (entry.Prefab == null)
                    Debug.LogError($"[CombatEffectCatalogSO] {name} 的 {entry.Id} 未配置预制体。", this);
                entry.MaximumRetained = Mathf.Max(1, entry.MaximumRetained);
                entry.PrewarmCount = Mathf.Clamp(entry.PrewarmCount, 0, entry.MaximumRetained);
            }
        }
#endif
    }
}
