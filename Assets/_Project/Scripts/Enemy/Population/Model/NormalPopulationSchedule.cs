using System;

namespace EnemySystem
{
    /// <summary>按配置速度将普通敌人补充到目标人口。</summary>
    public sealed class NormalPopulationSchedule
    {
        private const double MinimumSpawnRatePerSecond = 0.01d;
        private const double MinimumRefillRatio = 0.5d;
        private const double MaximumRefillRatio = 1d;
        private const double MinimumElapsedTolerance = 0.000001d;

        private readonly int _target;
        private readonly double _spawnInterval;
        private readonly double _refillRatio;
        private double _elapsed;

        /// <summary>创建使用指定人口目标、生成速度和补充阈值的调度器。</summary>
        /// <param name="target">普通敌人目标存活数。</param>
        /// <param name="spawnRatePerSecond">人口补充期间的最大生成速度。</param>
        /// <param name="refillRatio">低于目标人口的此比例时开始补充。</param>
        public NormalPopulationSchedule(int target, float spawnRatePerSecond, float refillRatio)
        {
            _target = Math.Max(0, target);
            double safeRate = IsFinite(spawnRatePerSecond)
                ? Math.Max(MinimumSpawnRatePerSecond, spawnRatePerSecond)
                : MinimumSpawnRatePerSecond;
            _spawnInterval = 1d / safeRate;

            _refillRatio = IsFinite(refillRatio)
                ? Math.Max(MinimumRefillRatio, Math.Min(MaximumRefillRatio, refillRatio))
                : MinimumRefillRatio;
        }

        /// <summary>读取当前普通怪物人口目标。</summary>
        public int Target => _target;

        /// <summary>推进补充计时，达到补充阈值后暂停生成并清空计时。</summary>
        public void Tick(float deltaTime, Func<int> count, Func<bool> spawn)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ||
                count == null || spawn == null || _target == 0)
            {
                return;
            }

            int currentCount = Math.Max(0, count());
            if (currentCount >= _target * _refillRatio)
            {
                _elapsed = 0d;
                return;
            }

            _elapsed += deltaTime;
            while (_elapsed + MinimumElapsedTolerance >= _spawnInterval && count() < _target)
            {
                if (!spawn())
                {
                    _elapsed = Math.Min(_elapsed, _spawnInterval);
                    break;
                }

                _elapsed -= _spawnInterval;
            }

            if (count() >= _target)
            {
                _elapsed = 0d;
            }
        }

        /// <summary>判断生成参数是否为有限浮点值。</summary>
        /// <param name="value">待检查的配置值。</param>
        /// <returns>值不是 NaN 或无穷大时返回 true。</returns>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
