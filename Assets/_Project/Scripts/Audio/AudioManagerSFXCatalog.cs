using UnityEngine;

namespace GameSystem
{
    /// <summary>解析音效目录覆盖项；音源池和请求处理仍由 AudioManager 持有。</summary>
    public partial class AudioManager
    {
        [Header("音效目录")]
        [SerializeField] private SoundCatalogSO _soundCatalog;

        /// <summary>读取目录倍率与空间模式，未登记 Clip 时保留调用点默认值。</summary>
        /// <param name="clip">本次请求已经选定的 Clip。</param>
        /// <param name="fallbackSpatialBlend">调用点原有的空间模式。</param>
        /// <param name="catalogVolume">目录音量倍率；未命中时为一。</param>
        /// <param name="spatialBlend">目录空间模式或调用点默认模式。</param>
        private void ResolveSfxSettings(
            AudioClip clip,
            float fallbackSpatialBlend,
            out float catalogVolume,
            out float spatialBlend)
        {
            catalogVolume = 1f;
            spatialBlend = fallbackSpatialBlend;

            if (_soundCatalog != null && _soundCatalog.TryGet(clip, out SoundCatalogEntry entry))
            {
                catalogVolume = Mathf.Clamp01(entry.Volume);
                spatialBlend = Mathf.Clamp01(entry.SpatialBlend);
            }
        }
    }
}
