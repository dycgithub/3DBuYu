using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 记录每个炮台端口的冷却。炮台 ID 参与键值，避免多个炮台互相影响。
    /// </summary>
    public sealed class AttackCooldownRegistry
    {
        private readonly IAttackClock _clock;
        private readonly Dictionary<long, float> _lastAttackTimes = new();

        public AttackCooldownRegistry(IAttackClock clock = null)
        {
            _clock = clock ?? new UnityAttackClock();
        }

        public bool IsReady(int sourceId, int portIndex, float fireRate)
        {
            long key = MakeKey(sourceId, portIndex);
            if (!_lastAttackTimes.TryGetValue(key, out float lastAttackTime))
                return true;

            float interval = 1f / Mathf.Max(0.001f, fireRate);
            return _clock.Time - lastAttackTime >= interval;
        }

        public void MarkUsed(int sourceId, int portIndex)
        {
            _lastAttackTimes[MakeKey(sourceId, portIndex)] = _clock.Time;
        }

        public void Clear()
        {
            _lastAttackTimes.Clear();
        }

        private static long MakeKey(int sourceId, int portIndex)
        {
            return ((long)sourceId << 32) ^ (uint)portIndex;
        }
    }
}
