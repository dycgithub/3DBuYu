using System;

namespace ItemSystem
{
    [Serializable]
    public struct DurabilityState
    {
        public int instanceId;
        public int maxDurability;
        public int currentDurability;
        public float maxUsageTime;
        public float remainingTime;
        public bool isBroken;
        public bool isExpired;

        public bool HasDurability => maxDurability > 0;
        public bool HasUsageTime => maxUsageTime > 0f;
        public float DurabilityPercent => maxDurability > 0
            ? (float)currentDurability / maxDurability
            : 1f;
        public float TimePercent => maxUsageTime > 0f
            ? remainingTime / maxUsageTime
            : 1f;
    }
}
