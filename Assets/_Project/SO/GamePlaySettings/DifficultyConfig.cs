using UnityEngine;

namespace GameSystem
{
    [CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Game/Difficulty Config")]
    public class DifficultyConfig : ScriptableObject
    {
        [Header("数量难度")]
        [SerializeField] private DifficultyLevel level = DifficultyLevel.Easy;

        /// <summary>数量配额与初始轨道半径使用同一难度序号。</summary>
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

        [Min(0f)]
        [SerializeField] private float spawnPressureMultiplier = 1f;

        public string DifficultyId => difficultyId;
        public float SettlementMultiplier => settlementMultiplier;
        public float EnemyHealthMultiplier => enemyHealthMultiplier;
        public float EnemySpeedMultiplier => enemySpeedMultiplier;
        public float SpawnPressureMultiplier => spawnPressureMultiplier;
    }
}
