using System;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>普通怪物人口选择项，权重用于归一化后的随机选择。</summary>
    [Serializable]
    public sealed class EnemySpawnOption
    {
        public Enemy prefab;
        [Min(0f)] public float weight = 1f;

        public EnemySpawnOption()
        {
        }

        public EnemySpawnOption(Enemy prefab, float weight)
        {
            this.prefab = prefab;
            this.weight = weight;
        }

        public Enemy Prefab => prefab;
        public float Weight => weight;
    }
}
