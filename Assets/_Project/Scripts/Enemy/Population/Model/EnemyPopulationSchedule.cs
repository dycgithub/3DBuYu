using System;

namespace EnemySystem
{
    /// <summary>累计首次生成配额，低于八成后按各类速率持续补充到上限。</summary>
    public sealed class EnemyPopulationSchedule
    {
        private const double InitialSeconds = 5d;
        private const int RankCount = 3;
        private static readonly double[] Intervals = { 0.02d, 5d, 60d };
        private readonly EnemyQuota _quota;
        private readonly int[] _initialSpawned = new int[RankCount];
        private readonly double[] _elapsed = new double[RankCount];
        private double _initialElapsed;

        public bool InitialCompleted { get; private set; }
        public bool IsRefilling { get; private set; }

        public EnemyPopulationSchedule(EnemyQuota quota)
        {
            _quota = quota;
        }

        /// <summary>
        /// 以当前存活数量作为首次铺怪基线创建新调度。
        /// </summary>
        /// <param name="quota">新的敌人配额。</param>
        /// <param name="existingCounts">按生成分类排列的当前存活数量。</param>
        public EnemyPopulationSchedule(EnemyQuota quota, int[] existingCounts)
            : this(quota)
        {
            if (existingCounts == null || existingCounts.Length < RankCount)
            {
                return;
            }

            bool initialCompleted = true;
            double initialProgress = 0d;
            for (int index = 0; index < RankCount; index++)
            {
                int existingCount = Math.Max(0, existingCounts[index]);
                int limit = _quota.GetLimit((EnemyRank)index);
                _initialSpawned[index] = Math.Min(existingCount, limit);
                initialCompleted &= existingCount >= limit;

                if (limit > 0)
                {
                    initialProgress = Math.Max(
                        initialProgress,
                        (double)_initialSpawned[index] / limit);
                }
            }

            _initialElapsed = initialCompleted
                ? InitialSeconds
                : InitialSeconds * initialProgress;
            InitialCompleted = initialCompleted;
        }

        /// <summary>按实际经过时间生成；回调成功才消费配额，失败留到后续帧重试。</summary>
        public void Tick(float deltaTime, Func<EnemyRank, int> count, Func<EnemyRank, bool> spawn)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            if (!InitialCompleted)
            {
                _initialElapsed = Math.Min(InitialSeconds, _initialElapsed + deltaTime);
                bool completed = _initialElapsed >= InitialSeconds;
                for (int index = 0; index < RankCount; index++)
                {
                    EnemyRank rank = (EnemyRank)index;
                    int limit = _quota.GetLimit(rank);
                    int due = (int)Math.Floor(limit * _initialElapsed / InitialSeconds + 0.000001d);
                    while (_initialSpawned[index] < due && count(rank) < limit)
                    {
                        if (!spawn(rank))
                        {
                            break;
                        }
                        _initialSpawned[index]++;
                    }
                    completed &= _initialSpawned[index] >= limit;
                }
                InitialCompleted = completed;
                return;
            }

            int total = count(EnemyRank.Normal) + count(EnemyRank.Elite) + count(EnemyRank.Boss);
            if (!IsRefilling)
            {
                if ((long)total * 5 >= (long)_quota.Total * 4)
                {
                    return;
                }
                IsRefilling = true;
                Array.Clear(_elapsed, 0, RankCount);
            }

            bool full = true;
            for (int index = 0; index < RankCount; index++)
            {
                EnemyRank rank = (EnemyRank)index;
                int limit = _quota.GetLimit(rank);
                if (count(rank) >= limit)
                {
                    _elapsed[index] = 0d;
                    continue;
                }
                _elapsed[index] += deltaTime;
                while (_elapsed[index] + 0.000001d >= Intervals[index] && count(rank) < limit)
                {
                    if (!spawn(rank))
                    {
                        _elapsed[index] = Math.Min(_elapsed[index], Intervals[index]);
                        break;
                    }
                    _elapsed[index] -= Intervals[index];
                }
                if (count(rank) >= limit)
                {
                    _elapsed[index] = 0d;
                }
                else
                {
                    full = false;
                }
            }
            IsRefilling = !full;
        }
    }
}
