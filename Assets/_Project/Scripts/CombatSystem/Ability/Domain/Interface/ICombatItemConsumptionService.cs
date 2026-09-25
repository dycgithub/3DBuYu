namespace CombatSystem
{
    /// <summary>
    /// 查询并消费库存中战斗物品实例。
    /// </summary>
    public interface ICombatItemConsumptionService
    {
        /// <summary>
        /// 预检查物品实例是否可以被找到和消费，不修改库存。</summary>
        /// <param name="itemInstanceId">物品实例标识。</param>
        /// <returns>找到可消费实例时返回 <c>true</c>。</returns>
        bool CanConsumeItemInstance(int itemInstanceId);

        /// <summary>
        /// 尝试从库存中移除一个物品实例。</summary>
        /// <param name="itemInstanceId">要移除的物品实例标识。</param>
        /// <returns>实际移除成功时返回 <c>true</c>。</returns>
        bool TryConsumeItemInstance(int itemInstanceId);
    }
}
