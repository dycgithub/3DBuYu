using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSystem
{
    /// <summary>一条 BGM 类型到曲目音频及曲目音量的配置。</summary>
    [Serializable]
    public sealed class MusicCatalogEntry
    {
        public BGMType Id;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;
    }

    /// <summary>集中配置 BGM 曲目，不负责播放。</summary>
    [CreateAssetMenu(fileName = "MusicCatalog", menuName = "Audio/Music Catalog")]
    public sealed class MusicCatalogSO : ScriptableObject
    {
        [SerializeField] private List<MusicCatalogEntry> _entries = new();

        public IReadOnlyList<MusicCatalogEntry> Entries => _entries;

        /// <summary>按现有 BGM 类型查找曲目配置。</summary>
        /// <param name="id">要查找的 BGM 类型。</param>
        /// <param name="entry">找到的曲目配置；未找到时为 null。</param>
        /// <returns>存在有效类型配置时返回 true。</returns>
        public bool TryGet(BGMType id, out MusicCatalogEntry entry)
        {
            if (id == BGMType.None)
            {
                entry = null;
                return false;
            }

            for (int index = 0; index < _entries.Count; index++)
            {
                MusicCatalogEntry candidate = _entries[index];
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
        /// <summary>检查目录键和片段，并将曲目音量限制在有效范围。</summary>
        private void OnValidate()
        {
            if (_entries == null)
            {
                _entries = new List<MusicCatalogEntry>();
            }

            HashSet<BGMType> ids = new();
            for (int index = 0; index < _entries.Count; index++)
            {
                MusicCatalogEntry entry = _entries[index];
                if (entry == null)
                {
                    continue;
                }

                if (entry.Id == BGMType.None)
                {
                    Debug.LogError($"[MusicCatalogSO] {name} 不能使用 None 作为配置键。", this);
                }

                if (!ids.Add(entry.Id))
                {
                    Debug.LogError($"[MusicCatalogSO] {name} 存在重复配置键: {entry.Id}。", this);
                }

                if (entry.Clip == null)
                {
                    Debug.LogError($"[MusicCatalogSO] {name} 的 {entry.Id} 未配置音频片段。", this);
                }

                entry.Volume = Mathf.Clamp01(entry.Volume);
            }
        }
#endif
    }
}
