using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 关卡单局参数。
    /// 时间字段表示目标时长；实际结算规则由战局服务处理。
    /// </summary>
    [CreateAssetMenu(fileName = "StageConfig", menuName = "Game/Stage Config")]
    public class StageConfig : ScriptableObject
    {
        [Tooltip("单局目标时长（秒）。达到后进入超时阶段。")]
        public float timeLimit = 300f;

        [Tooltip("成功结算所需的最低击杀数。0 表示暂不限制。")]
        [Min(0)]
        public int targetKillCount;

        [Header("能量")]
        [Tooltip("本局开始时的能量。实际值会被最大能量限制。")]
        [Min(0f)]
        public float initialEnergy = 100f;

        [Tooltip("本局能量上限。")]
        [Min(0f)]
        public float maxEnergy = 100f;

        [Tooltip("本局每经过 1 秒消耗的基础能量；实际消耗会乘当前超时倍率。")]
        [Min(0f)]
        public float baseEnergyDrainPerSecond = 1f;

        [Header("默认难度")]
        [Tooltip("未由关卡选择界面覆盖时使用的难度配置。")]
        public DifficultyConfig defaultDifficulty;
    }
}
