namespace CombatSystem
{
    /// <summary>
    /// 场景级 Buff 运行时服务。
    /// 所有目标共享同一个服务，目标自身不维护 Buff Controller 或运行时列表。
    /// </summary>
    public interface IBuffService
    {
        /// <summary>登记一个当前可用的 Buff 目标。</summary>
        /// <param name="target">待登记的目标。</param>
        void RegisterTarget(IBuffTarget target);

        /// <summary>注销一个目标并清理其所有运行时效果。</summary>
        /// <param name="target">待注销的目标。</param>
        void UnregisterTarget(IBuffTarget target);

        /// <summary>向指定目标应用一个 Buff。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="definition">Buff 静态定义。</param>
        /// <param name="sourceInstanceId">Buff 来源实例标识。</param>
        void ApplyBuff(IBuffTarget target, BuffDefinitionSO definition, int sourceInstanceId);

        /// <summary>施加一个独立计时的火焰持续伤害层。</summary>
        /// <param name="target">持续伤害目标。</param>
        /// <param name="damagePerSecond">该层每秒伤害。</param>
        /// <param name="durationSeconds">该层持续时间。</param>
        /// <param name="sourceInstanceId">来源物品实例标识。</param>
        void ApplyDamageOverTime(
            IBuffTarget target,
            float damagePerSecond,
            float durationSeconds,
            int sourceInstanceId);

        /// <summary>
        /// 仅在目标没有同类活动 Buff 时新增一个 Buff。
        /// </summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="definition">Buff 静态定义。</param>
        /// <param name="sourceInstanceId">Buff 来源实例标识。</param>
        /// <returns>实际新增时返回 true。</returns>
        bool TryApplyBuffIfAbsent(
            IBuffTarget target,
            BuffDefinitionSO definition,
            int sourceInstanceId);

        /// <summary>查询目标指定属性的 Buff 合成倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="modifierType">属性类型。</param>
        /// <returns>没有匹配 Buff 时返回一。</returns>
        float GetModifierMultiplier(IBuffTarget target, BuffModifierType modifierType);

        /// <summary>移除目标上指定来源创建的所有临时 Buff。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        void RemoveBySourceId(IBuffTarget target, int sourceInstanceId);

        /// <summary>清除目标上的所有临时 Buff。</summary>
        /// <param name="target">Buff 目标。</param>
        void ClearAllBuffs(IBuffTarget target);

        /// <summary>设置目标指定来源的永久移动速度倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="multiplier">移动速度倍率。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        void SetPermanentMovementSpeedMultiplier(
            IBuffTarget target,
            float multiplier,
            int sourceInstanceId);

        /// <summary>移除目标指定来源的永久移动速度倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        void ClearPermanentMovementSpeedMultiplier(
            IBuffTarget target,
            int sourceInstanceId);

        /// <summary>清除目标的全部永久移动速度倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        void ClearAllPermanentMovementSpeedMultipliers(IBuffTarget target);

        /// <summary>按来源清除场景内所有目标的永久移动速度倍率。</summary>
        /// <param name="sourceInstanceId">永久效果来源实例标识。</param>
        void ClearPermanentMovementSpeedMultipliersBySourceId(int sourceInstanceId);
    }
}
