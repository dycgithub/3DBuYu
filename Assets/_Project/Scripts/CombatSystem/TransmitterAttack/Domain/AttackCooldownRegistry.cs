using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 按攻击来源和端口维护独立的攻击冷却时间。
    /// </summary>
    public sealed class AttackCooldownRegistry
    {
        private readonly IAttackClock _clock;
        private readonly Dictionary<long, float> _lastAttackTimes = new();

        /// <summary>创建攻击冷却注册表。</summary>
        /// <param name="clock">可选的时间来源；为空时使用 Unity 缩放时间。</param>
        public AttackCooldownRegistry(IAttackClock clock = null)
        {
            _clock = clock ?? new UnityAttackClock();
        }

        /// <summary>检查指定端口当前是否可以提交攻击。</summary>
        /// <param name="sourceInstanceId">发起攻击的对象实例 ID。</param>
        /// <param name="transmitterPortIndex">发射器端口索引。</param>
        /// <param name="attacksPerSecond">端口每秒允许的攻击次数。</param>
        /// <returns>没有历史冷却或冷却已经结束时返回 <c>true</c>。</returns>
        public bool IsAttackReady(int sourceInstanceId, int transmitterPortIndex, float attacksPerSecond)
        {
            long key = CreateCooldownKey(sourceInstanceId, transmitterPortIndex);
            if (!_lastAttackTimes.TryGetValue(key, out float lastAttackTime))
            {
                return true;
            }

            float interval = 1f / Mathf.Max(0.001f, attacksPerSecond);
            return _clock.CurrentTimeSeconds - lastAttackTime >= interval;
        }

        /// <summary>记录指定端口刚刚提交了一次攻击。</summary>
        /// <param name="sourceInstanceId">发起攻击的对象实例 ID。</param>
        /// <param name="transmitterPortIndex">发射器端口索引。</param>
        public void RecordAttack(int sourceInstanceId, int transmitterPortIndex)
        {
            _lastAttackTimes[CreateCooldownKey(sourceInstanceId, transmitterPortIndex)] = _clock.CurrentTimeSeconds;
        }

        /// <summary>清除全部端口的冷却记录。</summary>
        public void ClearAllCooldowns() => _lastAttackTimes.Clear();

        /// <summary>将来源实例和端口索引组合为字典键。</summary>
        /// <remarks>高低 32 位分别保存两个整数，避免字符串键分配。</remarks>
        private static long CreateCooldownKey(int sourceInstanceId, int transmitterPortIndex)
        {
            return ((long)sourceInstanceId << 32) ^ (uint)transmitterPortIndex;
        }
    }
}
