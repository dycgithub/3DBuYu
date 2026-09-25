using System;
using System.Collections.Generic;

namespace EnemySystem
{
    /// <summary>根据权重选择普通怪物预制体，不持有随机状态。</summary>
    public static class NormalEnemySelector
    {
        /// <summary>使用 [0,1] 范围的采样值按权重选择预制体。</summary>
        public static Enemy Select(IReadOnlyList<EnemySpawnOption> options, float sample)
        {
            if (options == null || options.Count == 0)
            {
                return null;
            }

            double totalWeight = 0d;
            for (int index = 0; index < options.Count; index++)
            {
                EnemySpawnOption option = options[index];
                if (option != null && option.Prefab != null && option.Weight > 0f &&
                    !float.IsNaN(option.Weight) && !float.IsInfinity(option.Weight))
                {
                    totalWeight += option.Weight;
                }
            }

            if (totalWeight <= 0d)
            {
                return null;
            }

            float normalizedSample = float.IsNaN(sample) || float.IsInfinity(sample)
                ? 0f
                : Math.Max(0f, Math.Min(1f, sample));
            double threshold = normalizedSample * totalWeight;
            Enemy lastPrefab = null;

            for (int index = 0; index < options.Count; index++)
            {
                EnemySpawnOption option = options[index];
                if (option == null || option.Prefab == null || option.Weight <= 0f ||
                    float.IsNaN(option.Weight) || float.IsInfinity(option.Weight))
                {
                    continue;
                }

                lastPrefab = option.Prefab;
                threshold -= option.Weight;
                if (threshold < 0d)
                {
                    return option.Prefab;
                }
            }

            return lastPrefab;
        }
    }
}
