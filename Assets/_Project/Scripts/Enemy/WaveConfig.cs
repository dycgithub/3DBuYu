using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 波次配置集合 — 持有所有 <see cref="WaveData"/>。
    /// 不再支持循环模式(loopMode 已移除),按 waves 列表顺序推进,完成后由 IWaveEventService 发布 OnAllWavesCompleted 事实。
    /// </summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "Game/Wave Config")]
    public class WaveConfig : ScriptableObject
    {
        [Header("波次列表")]
        [Tooltip("所有波次数据,按列表顺序依次进行")]
        public List<WaveData> waves = new List<WaveData>();

        [Header("全局设置")]
        [Tooltip("初始波次编号(1 起)")]
        public int startWave = 1;

        /// <summary>总波次数。</summary>
        public int GetTotalWaveCount() => waves != null ? waves.Count : 0;

        /// <summary>获取指定波次(1-based)。越界返回 null。</summary>
        public WaveData GetWave(int waveNumber)
        {
            if (waves == null) return null;
            if (waveNumber < 1 || waveNumber > waves.Count) return null;
            return waves[waveNumber - 1];
        }
    }
}
