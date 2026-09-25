using System;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>单次精英或 Boss 倒计时生成规则。</summary>
    [Serializable]
    public sealed class SpecialEnemySpawnRule
    {
        public string Id = "special-1";
        public EnemySpawnCategory Category = EnemySpawnCategory.Elite;
        public Enemy Prefab;
        [Min(0f)] public float TriggerTimeSeconds;

        /// <summary>判断规则是否可以进入时间表。</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(Id) &&
            (Category == EnemySpawnCategory.Elite || Category == EnemySpawnCategory.Boss) &&
            Prefab != null &&
            TriggerTimeSeconds >= 0f &&
            !float.IsNaN(TriggerTimeSeconds) &&
            !float.IsInfinity(TriggerTimeSeconds);
    }
}
