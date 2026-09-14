namespace CombatSystem
{
    /// <summary>
    /// 接收完整伤害请求并返回目标专属处理结果的契约。
    /// </summary>
    public interface IDamageRequestReceiver
    {
        /// <summary>
        /// 根据伤害请求执行目标自身的伤害规则。
        /// </summary>
        /// <param name="request">包含来源、伤害量、类型和命中信息的只读请求。</param>
        /// <returns>目标处理后的结果；结果状态区分无效、闪避、格挡、命中和击杀。</returns>
        DamageResult ReceiveDamageRequest(in DamageRequest request);
    }
}
