using System.Collections.Generic;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 关卡单局参数。
    /// 默认目标时长为三分钟；达到后保留超时机制，实际结算规则由战局服务处理。
    /// </summary>
    [CreateAssetMenu(fileName = "StageConfig", menuName = "Game/Stage Config")]
    public class StageConfig : ScriptableObject
    {
        [Tooltip("单局目标时长（秒）。达到后进入超时阶段。")]
        public float timeLimit = 180f;

        [Tooltip("成功结算所需的最低击杀数。0 表示暂不限制。")]
        [Min(0)]
        public int targetKillCount;

        [Header("能量")]
        [Tooltip("本局开始时的能量。实际值会被最大能量限制。")]
        [Min(0f)]
        public float initialEnergy = 500f;

        [Tooltip("本局能量上限。")]
        [Min(0f)]
        public float maxEnergy = 1000f;

        [Tooltip("本局每经过 1 秒消耗的基础能量；实际消耗会乘当前超时倍率。")]
        [Min(0f)]
        public float baseEnergyDrainPerSecond = 2f;

        [Header("超时")]
        [Tooltip("进入超时时使用的初始能量消耗倍率。")]
        [Min(0f)]
        public float overtimeStartMultiplier = 1.5f;

        [Tooltip("超时倍率增加一次所需的时间（秒）。")]
        [Min(0.01f)]
        public float overtimeMultiplierIntervalSeconds = 15f;

        [Tooltip("每经过一次超时倍率间隔增加的倍率。")]
        [Min(0f)]
        public float overtimeMultiplierIncrement = 0.25f;

        [Tooltip("超时能量消耗倍率上限，避免超时积分和能量消耗无限增长。")]
        [Min(0f)]
        public float overtimeMultiplierMaximum = 3f;

        [Header("连杀")]
        [Tooltip("两次击杀之间允许保持连杀的最长间隔（秒）。")]
        [Min(0f)]
        public float killStreakWindowSeconds = 3f;

        [Header("默认难度")]
        [Tooltip("未由关卡选择界面覆盖时使用的难度配置。")]
        public DifficultyConfig defaultDifficulty;

        [Header("可用难度")]
        [Tooltip("本局可切换的难度配置；数组顺序不影响难度高低。")]
        [SerializeField] private DifficultyConfig[] availableDifficulties;

        /// <summary>获取本局可用的难度配置列表。</summary>
        public IReadOnlyList<DifficultyConfig> AvailableDifficulties => availableDifficulties;
    }
}
