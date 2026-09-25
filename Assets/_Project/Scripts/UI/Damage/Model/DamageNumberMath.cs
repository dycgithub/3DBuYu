using UnityEngine;

namespace _Project.UI.Damage
{
    /// <summary>提供跳字聚合、优先级比较和稳定偏移的无状态规则。</summary>
    public static class DamageNumberMath
    {
        /// <summary>判断两次伤害是否属于同一条普通跳字。</summary>
        public static bool CanAggregate(
            int existingTargetId,
            int incomingTargetId,
            DamageNumberPriority existingPriority,
            DamageNumberPriority incomingPriority,
            float elapsed,
            float window)
        {
            return existingTargetId == incomingTargetId &&
                existingPriority == incomingPriority &&
                existingPriority == DamageNumberPriority.Normal &&
                elapsed >= 0f &&
                elapsed <= Mathf.Max(0f, window);
        }

        /// <summary>判断新优先级是否可以替换旧的低优先级跳字。</summary>
        public static bool HasHigherPriority(
            DamageNumberPriority incoming,
            DamageNumberPriority existing)
        {
            return incoming > existing;
        }

        /// <summary>根据目标和序号生成可复现的屏幕偏移。</summary>
        public static Vector2 GetOffset(int targetId, int sequence)
        {
            uint seed = (uint)targetId * 747796405u + (uint)sequence * 2891336453u;
            seed = (seed ^ (seed >> 16)) * 2246822519u;
            seed ^= seed >> 13;
            float horizontal = (seed & 1023u) / 1023f * 2f - 1f;
            float vertical = ((seed >> 10) & 1023u) / 1023f * 2f - 1f;
            return new Vector2(horizontal, vertical);
        }

        /// <summary>根据伤害是否暴击映射首期跳字优先级。</summary>
        public static DamageNumberPriority GetPriority(bool wasCritical)
        {
            return wasCritical
                ? DamageNumberPriority.Critical
                : DamageNumberPriority.Normal;
        }
    }
}
