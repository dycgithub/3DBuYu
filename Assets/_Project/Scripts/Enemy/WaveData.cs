using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 单波次数据 — 由「触发器」与「生成计划」两段组成。
    /// <para>触发器 (<see cref="trigger"/>) 决定何时开始本波次。</para>
    /// <para>生成计划 (<see cref="enemies"/>) 决定本波次生成什么敌人、何时生成。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "WaveData", menuName = "Game/Wave Data")]
    public class WaveData : ScriptableObject
    {
        [Header("波次信息")]
        [Tooltip("波次编号(1 起)")]
        public int waveNumber = 1;

        [Tooltip("波次名称")]
        public string waveName = "第一波";

        [Tooltip("波次描述")]
        [TextArea(2, 4)]
        public string description = "普通敌人组成的基础波次";

        [Header("触发器")]
        [Tooltip("决定本波次何时开始")]
        public WaveTrigger trigger = new WaveTrigger();

        [Header("生成计划")]
        [Tooltip("本波次包含的敌人生成组")]
        public List<WaveEnemyInfo> enemies = new List<WaveEnemyInfo>();

        [Header("时间设置(秒)")]
        [Tooltip("波次开始前的准备/倒计时时间")]
        public float preparationTime = 5f;

        [Tooltip("清场后到下一波开始的延迟")]
        public float clearDelay = 3f;

        [Header("难度倍率(作用于敌人 Stats)")]
        [Tooltip("敌人血量倍数(乘到 EnemyStats.baseHealth)")]
        public float healthMultiplier = 1f;

        [Tooltip("敌人速度倍数(乘到 EnemyStats.baseSpeed)")]
        public float speedMultiplier = 1f;

        /// <summary>本波次总敌人数(各组 spawnCount 之和)。</summary>
        public int GetTotalEnemyCount()
        {
            int total = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e != null) total += Mathf.Max(0, e.spawnCount);
            }
            return total;
        }

        /// <summary>本波次预估生成时长(从开始到所有敌人生成的时长)。</summary>
        public float GetEstimatedSpawnDuration()
        {
            float maxDuration = 0f;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null) continue;
                float duration = e.delayStart + Mathf.Max(0, e.spawnCount - 1) * e.spawnInterval;
                if (duration > maxDuration) maxDuration = duration;
            }
            return maxDuration;
        }
    }
}
