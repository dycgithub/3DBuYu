namespace ItemSystem
{
    [System.Serializable]
    public class ItemDurabilityData
    {
        public float maxUsageTime;
        public int maxDurability;
        public int durabilityPerShot = 1;
        public bool destroyOnBreak = true;
    }
}
