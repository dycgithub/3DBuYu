using System;
using System.Collections.Generic;

namespace EnemySystem.AreaEffect.Model
{
    /// <summary>按调用方时间节流效果，并轮转有界的候选窗口。</summary>
    internal sealed class EnemyAreaEffectScheduler
    {
        internal const int MaximumTargets = 50;

        private const float MinimumTickIntervalSeconds = 0.01f;

        private bool _hasNextTick;
        private float _nextTickTime;
        private int _candidateStartIndex;

        /// <summary>根据重置后的累计时间判断周期是否到期，同一时刻至多开启一次。</summary>
        /// <param name="currentTime">调度状态重置后由调用方累计的时间。</param>
        /// <param name="interval">两次效果周期之间的秒数。</param>
        /// <returns>到期时返回 true，同一时刻至多返回一次。</returns>
        internal bool TryBeginTick(float currentTime, float interval)
        {
            float safeInterval = Math.Max(MinimumTickIntervalSeconds, interval);
            if (!_hasNextTick)
            {
                _hasNextTick = true;
                _nextTickTime = safeInterval;
            }

            if (currentTime < _nextTickTime)
            {
                return false;
            }

            _nextTickTime = currentTime + safeInterval;
            return true;
        }

        /// <summary>复制至多五十个候选，并逐个推进下次窗口起点。</summary>
        /// <param name="candidates">空间查询返回的完整候选列表。</param>
        /// <param name="window">接收本周期候选的复用列表。</param>
        internal void CopyCandidateWindow<T>(IReadOnlyList<T> candidates, List<T> window)
        {
            window.Clear();
            int candidateCount = candidates.Count;
            if (candidateCount == 0)
            {
                _candidateStartIndex = 0;
                return;
            }

            int startIndex = _candidateStartIndex % candidateCount;
            int windowCount = Math.Min(MaximumTargets, candidateCount);
            for (int offset = 0; offset < windowCount; offset++)
            {
                window.Add(candidates[(startIndex + offset) % candidateCount]);
            }

            _candidateStartIndex = (startIndex + 1) % candidateCount;
        }

        /// <summary>重置到期时刻和候选窗口，供对象池重新租借时使用。</summary>
        internal void Reset()
        {
            _hasNextTick = false;
            _nextTickTime = 0f;
            _candidateStartIndex = 0;
        }
    }
}
