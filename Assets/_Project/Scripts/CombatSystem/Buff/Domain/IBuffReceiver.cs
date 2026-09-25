namespace CombatSystem
{
    /// <summary>
    /// 可以被施加Buff。
    /// </summary>
    public interface IBuffReceiver
    {
        /// <summary>
        /// 以默认来源向目标应用一个 Buff。
        /// </summary>
        /// <param name="definition">要应用的 Buff 静态定义；为空时不产生效果。</param>
        void ApplyBuff(BuffDefinitionSO definition);

        /// <summary>
        /// 以指定来源向目标应用一个 Buff。
        /// </summary>
        /// <param name="definition">要应用的 Buff 静态定义；为空时不产生效果。</param>
        /// <param name="sourceInstanceId">Buff 来源实例标识，用于叠加、区分和按来源移除。</param>
        void ApplyBuff(BuffDefinitionSO definition, int sourceInstanceId);

        /// <summary>
        /// 仅在目标没有同类活动 Buff 时施加一个 Buff。
        /// </summary>
        /// <param name="definition">要施加的 Buff 静态定义。</param>
        /// <param name="sourceInstanceId">Buff 来源实例标识。</param>
        /// <returns>本次实际新增 Buff 时返回 true。</returns>
        bool TryApplyBuffIfAbsent(BuffDefinitionSO definition, int sourceInstanceId);
    }
}
