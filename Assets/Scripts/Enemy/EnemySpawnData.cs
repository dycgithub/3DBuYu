using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 波次中的敌人生成信息
    /// </summary>
    [System.Serializable]
    public class WaveEnemyInfo
    {
        [Tooltip("敌人类型")]
        public EnemyType enemyType;

        [Tooltip("敌人预制体")]
        public GameObject enemyPrefab;

        [Tooltip("生成数量")]
        public int spawnCount = 5;

        [Tooltip("生成间隔（秒）")]
        public float spawnInterval = 1f;

        [Tooltip("延迟开始生成（秒）")]
        public float delayStart = 0f;
    }

    /// <summary>
    /// 单波次数据
    /// </summary>
    [CreateAssetMenu(fileName = "WaveData", menuName = "Game/Wave Data")]
    public class WaveData : ScriptableObject
    {
        [Header("波次信息")]
        [Tooltip("波次编号")]
        public int waveNumber = 1;

        [Tooltip("波次名称")]
        public string waveName = "第一波";

        [Tooltip("波次描述")]
        [TextArea(2, 4)]
        public string description = "普通敌人组成的基础波次";

        [Header("敌人生成")]
        [Tooltip("本波次包含的敌人类型")]
        public List<WaveEnemyInfo> enemies = new List<WaveEnemyInfo>();

        [Header("时间设置")]
        [Tooltip("波次准备时间（秒）")]
        public float preparationTime = 5f;

        [Tooltip("波次最大持续时间（秒，0=无限制）")]
        public float maxDuration = 0f;

        [Tooltip("清场后延迟（秒）")]
        public float clearDelay = 3f;

        [Header("难度调整")]
        [Tooltip("敌人血量倍数")]
        public float healthMultiplier = 1f;

        [Tooltip("敌人伤害倍数")]
        public float damageMultiplier = 1f;

        [Tooltip("敌人速度倍数")]
        public float speedMultiplier = 1f;

        /// <summary>
        /// 获取本波次总敌人数
        /// </summary>
        public int GetTotalEnemyCount()
        {
            int total = 0;
            foreach (var enemy in enemies)
            {
                total += enemy.spawnCount;
            }
            return total;
        }

        /// <summary>
        /// 获取预估波次时长
        /// </summary>
        public float GetEstimatedDuration()
        {
            float maxDuration = 0f;
            foreach (var enemy in enemies)
            {
                float duration = enemy.delayStart + (enemy.spawnCount - 1) * enemy.spawnInterval;
                maxDuration = Mathf.Max(maxDuration, duration);
            }
            return maxDuration + preparationTime;
        }
    }

    /// <summary>
    /// 波次配置集合
    /// </summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "Game/Wave Config")]
    public class WaveConfig : ScriptableObject
    {
        [Header("波次列表")]
        [Tooltip("所有波次数据")]
        public List<WaveData> waves = new List<WaveData>();

        [Header("全局设置")]
        [Tooltip("初始波次编号")]
        public int startWave = 1;

        [Tooltip("循环模式（完成所有波次后是否从头开始）")]
        public bool loopMode = false;

        [Tooltip("循环后难度增量")]
        public float loopDifficultyIncrement = 0.2f;

        /// <summary>
        /// 获取指定波次
        /// </summary>
        public WaveData GetWave(int waveNumber)
        {
            // 处理循环模式
            if (loopMode && waveNumber > waves.Count)
            {
                int loopIndex = (waveNumber - 1) % waves.Count;
                WaveData wave = Instantiate(waves[loopIndex]);

                // 应用循环难度增量
                int loopCount = (waveNumber - 1) / waves.Count;
                float multiplier = 1f + loopCount * loopDifficultyIncrement;
                wave.healthMultiplier *= multiplier;
                wave.damageMultiplier *= multiplier;
                wave.waveNumber = waveNumber;

                return wave;
            }

            // 正常模式
            if (waveNumber > 0 && waveNumber <= waves.Count)
            {
                return waves[waveNumber - 1];
            }

            return null;
        }

        /// <summary>
        /// 获取总波次数
        /// </summary>
        public int GetTotalWaveCount()
        {
            return waves.Count;
        }
    }
}
