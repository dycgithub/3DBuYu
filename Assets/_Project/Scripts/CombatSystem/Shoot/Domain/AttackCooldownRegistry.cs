using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    public sealed class AttackCooldownRegistry
    {
        private readonly IAttackClock _clock;
        private readonly Dictionary<long, float> _lastAttackTimes = new();

        public AttackCooldownRegistry(IAttackClock clock = null)
        {
            _clock = clock ?? new UnityAttackClock();
        }

        public bool IsReady(int sourceId, int transmitterIndex, float fireRate)
        {
            long key = MakeKey(sourceId, transmitterIndex);
            if (!_lastAttackTimes.TryGetValue(key, out float lastAttackTime))
                return true;

            float interval = 1f / Mathf.Max(0.001f, fireRate);
            return _clock.Time - lastAttackTime >= interval;
        }

        public void MarkUsed(int sourceId, int transmitterIndex)
        {
            _lastAttackTimes[MakeKey(sourceId, transmitterIndex)] = _clock.Time;
        }

        public void Clear() => _lastAttackTimes.Clear();

        private static long MakeKey(int sourceId, int transmitterIndex)
        {
            return ((long)sourceId << 32) ^ (uint)transmitterIndex;
        }
    }
}
