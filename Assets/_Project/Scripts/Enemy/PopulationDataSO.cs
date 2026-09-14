using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 单个敌群数据，由「触发器」与「生成计划」两段组成。
    /// <para>触发器 (<see cref="trigger"/>) 决定何时开始本敌群。</para>
    /// <para>生成计划 (<see cref="enemies"/>) 决定本敌群生成什么敌人、何时生成。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "PopulationData", menuName = "Game/Population Data")]
    public class PopulationDataSO : ScriptableObject
    {
        [Header("敌群信息")]
        [Tooltip("敌群编号(1 起)")]
        public int populationNumber = 1;

        [Tooltip("敌群名称")]
        public string populationName = "第一群";

        [Tooltip("敌群描述")]
        [TextArea(2, 4)]
        public string description = "普通敌人组成的基础敌群";

        [Header("触发器")]
        [Tooltip("决定本敌群何时开始")]
        public PopulationTrigger trigger = new PopulationTrigger();

        [Header("生成计划")]
        [Tooltip("本敌群包含的敌人生成组")]
        public List<EnemySpawnGroup> enemies = new List<EnemySpawnGroup>();

        [Header("时间设置(秒)")]
        [Tooltip("敌群开始前的准备/倒计时时间")]
        public float preparationTime = 5f;

        [Tooltip("清场后到下一敌群开始的延迟")]
        public float clearDelay = 3f;

        [Header("难度倍率(作用于敌人运行时状态)")]
        [Tooltip("敌人生命值倍数，乘到 EnemyAttributesSO.baseHealth。")]
        public float healthMultiplier = 1f;

        [Tooltip("敌人速度倍数，传入 EnemyFlockSettingsSO 生成的运行时 Profile。")]
        public float speedMultiplier = 1f;

        /// <summary>本敌群总敌人数(各组 spawnCount 之和)。</summary>
        public int GetTotalEnemyCount()
        {
            int total = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemySpawnGroup spawnGroup = enemies[i];
                if (spawnGroup != null)
                {
                    total += Mathf.Max(0, spawnGroup.spawnCount);
                }
            }
            return total;
        }

        /// <summary>本敌群预估生成时长(从开始到所有敌人生成的时长)。</summary>
        public float GetEstimatedSpawnDuration()
        {
            float maxDuration = 0f;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemySpawnGroup spawnGroup = enemies[i];
                if (spawnGroup == null)
                {
                    continue;
                }

                float duration = spawnGroup.delayStart
                    + Mathf.Max(0, spawnGroup.spawnCount - 1) * spawnGroup.spawnInterval;
                if (duration > maxDuration)
                {
                    maxDuration = duration;
                }
            }
            return maxDuration;
        }
    }
}
