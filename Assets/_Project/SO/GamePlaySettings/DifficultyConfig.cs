using System.Collections.Generic;
using EnemySystem;
using UnityEngine;

namespace GameSystem
{
    [CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Game/Difficulty Config")]
    public class DifficultyConfig : ScriptableObject
    {
        [Header("数量难度")]
        [SerializeField] private DifficultyLevel level = DifficultyLevel.Easy;

        /// <summary>当前配置的难度等级。</summary>
        public DifficultyLevel Level => level;
        [Header("Identity")]
        [SerializeField] private string difficultyId = "normal";

        [Header("Settlement")]
        [Min(0f)]
        [SerializeField] private float settlementMultiplier = 1f;

        [Header("Enemy")]
        [Min(0f)]
        [SerializeField] private float enemyHealthMultiplier = 1f;

        [Min(0f)]
        [SerializeField] private float enemySpeedMultiplier = 1f;

        [Header("普通怪物人口")]
        [Min(0)]
        [SerializeField] private int normalPopulationTarget = 500;

        [SerializeField] private List<EnemySpawnOption> normalSpawnOptions = new();

        [Header("特殊怪物时间表")]
        [SerializeField] private List<SpecialEnemySpawnRule> specialSpawnRules = new();

        public string DifficultyId => difficultyId;
        public float SettlementMultiplier => settlementMultiplier;
        public float EnemyHealthMultiplier => enemyHealthMultiplier;
        public float EnemySpeedMultiplier => enemySpeedMultiplier;
        public int NormalPopulationTarget => normalPopulationTarget;
        public IReadOnlyList<EnemySpawnOption> NormalSpawnOptions => normalSpawnOptions;
        public IReadOnlyList<SpecialEnemySpawnRule> SpecialSpawnRules => specialSpawnRules;

        private void OnValidate()
        {
            normalPopulationTarget = Mathf.Max(0, normalPopulationTarget);

            if (normalSpawnOptions != null)
            {
                for (int index = 0; index < normalSpawnOptions.Count; index++)
                {
                    EnemySpawnOption option = normalSpawnOptions[index];
                    if (option == null)
                    {
                        Debug.LogError($"[DifficultyConfig] {name} 的普通怪物配置为空。", this);
                        continue;
                    }

                    if (option.Prefab == null || option.Weight <= 0f ||
                        float.IsNaN(option.Weight) || float.IsInfinity(option.Weight))
                    {
                        Debug.LogError($"[DifficultyConfig] {name} 的普通怪物权重配置无效。", this);
                    }
                }
            }

            if (specialSpawnRules == null)
            {
                return;
            }

            HashSet<string> ruleIds = new();
            for (int index = 0; index < specialSpawnRules.Count; index++)
            {
                SpecialEnemySpawnRule rule = specialSpawnRules[index];
                if (rule == null)
                {
                    Debug.LogError($"[DifficultyConfig] {name} 的特殊生成规则为空。", this);
                    continue;
                }

                if (!ruleIds.Add(rule.Id))
                {
                    Debug.LogError($"[DifficultyConfig] {name} 存在重复特殊生成规则 ID: {rule.Id}。", this);
                }

                if (!rule.IsValid)
                {
                    Debug.LogError($"[DifficultyConfig] {name} 的特殊生成规则无效: {rule.Id}。", this);
                }
            }
        }
    }
}
