namespace Services
{
    /// <summary>
    /// 提供本局能量消耗倍率和实际成本计算。
    /// </summary>
    public interface IEnergyCostService
    {
        /// <summary>当前所有能量消耗使用的倍率。</summary>
        float ConsumptionMultiplier { get; }

        /// <summary>设置当前所有能量消耗使用的倍率。</summary>
        /// <param name="multiplier">正数消耗倍率。</param>
        void SetConsumptionMultiplier(float multiplier);

        /// <summary>将基础消耗换算为当前阶段的实际消耗。</summary>
        /// <param name="baseAmount">未修正的基础消耗。</param>
        /// <returns>应用当前倍率后的实际消耗。</returns>
        float GetEffectiveCost(float baseAmount);
    }
}
