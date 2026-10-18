using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSystem
{
    /// <summary>为既有音效 Clip 配置统一的音量倍率和空间混合。</summary>
    [Serializable]
    public sealed class SoundCatalogEntry
    {
        [Tooltip("Inspector 中用于识别此音效用途的名称。")]
        public string Name;

        [Tooltip("稳定查找键；目录覆盖配置，不改变业务侧选用的 Clip。")]
        public AudioClip Clip;

        [Range(0f, 1f), Tooltip("与调用点原有音量相乘；默认 1 保留原倍率。")]
        public float Volume = 1f;

        [Range(0f, 1f), Tooltip("0 为二维，1 为完整三维空间音效。")]
        public float SpatialBlend;
    }

    /// <summary>集中配置游戏音效，不负责选择或播放音频。</summary>
    [CreateAssetMenu(fileName = "SoundCatalog", menuName = "Audio/Sound Catalog")]
    public sealed class SoundCatalogSO : ScriptableObject
    {
        [SerializeField] private List<SoundCatalogEntry> _entries = new();

        public IReadOnlyList<SoundCatalogEntry> Entries => _entries;

        /// <summary>使用业务请求的 Clip 引用查找统一音效配置。</summary>
        /// <param name="clip">业务侧已选中的音频片段。</param>
        /// <param name="entry">匹配的配置；未登记时为 null。</param>
        /// <returns>目录中存在该 Clip 的配置时返回 true。</returns>
        public bool TryGet(AudioClip clip, out SoundCatalogEntry entry)
        {
            if (clip != null)
            {
                for (int index = 0; index < _entries.Count; index++)
                {
                    SoundCatalogEntry candidate = _entries[index];
                    if (candidate != null && candidate.Clip == clip)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

#if UNITY_EDITOR
        /// <summary>检查空引用和重复 Clip，并限制目录倍率及空间混合范围。</summary>
        private void OnValidate()
        {
            if (_entries == null)
            {
                _entries = new List<SoundCatalogEntry>();
            }

            HashSet<AudioClip> clips = new();
            for (int index = 0; index < _entries.Count; index++)
            {
                SoundCatalogEntry entry = _entries[index];
                if (entry == null)
                {
                    continue;
                }

                if (entry.Clip == null)
                {
                    Debug.LogError($"[SoundCatalogSO] {name} 的 {entry.Name} 未配置音频片段。", this);
                }
                else if (!clips.Add(entry.Clip))
                {
                    Debug.LogError($"[SoundCatalogSO] {name} 存在重复 Clip: {entry.Clip.name}。", this);
                }

                entry.Volume = Mathf.Clamp01(entry.Volume);
                entry.SpatialBlend = Mathf.Clamp01(entry.SpatialBlend);
            }
        }
#endif
    }
}
