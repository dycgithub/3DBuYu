using System;

namespace EnemySystem
{
    /// <summary>普通人口低于八成后按固定速率补充到目标数量。</summary>
    public sealed class NormalPopulationSchedule
    {
        private const double SpawnInterval = 0.02d;
        private const int ThresholdPercent = 80;
        private const int Percent = 100;
        private readonly int _target;
        private double _elapsed;

        public NormalPopulationSchedule(int target)
        {
            _target = Math.Max(0, target);
        }

        /// <summary>读取当前普通怪物人口目标。</summary>
        public int Target => _target;

        /// <summary>推进补充计时，只有低于八成时才消耗生成机会。</summary>
        public void Tick(float deltaTime, Func<int> count, Func<bool> spawn)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ||
                count == null || spawn == null || _target == 0)
            {
                return;
            }

            int currentCount = Math.Max(0, count());
            if ((long)currentCount * Percent >= (long)_target * ThresholdPercent)
            {
                _elapsed = 0d;
                return;
            }

            _elapsed += deltaTime;
            while (_elapsed + 0.000001d >= SpawnInterval && count() < _target)
            {
                if (!spawn())
                {
                    _elapsed = Math.Min(_elapsed, SpawnInterval);
                    break;
                }

                _elapsed -= SpawnInterval;
            }

            if (count() >= _target)
            {
                _elapsed = 0d;
            }
        }
    }
}
