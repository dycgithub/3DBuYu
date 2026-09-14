using Interfaces;

namespace CombatSystem
{
    /// <summary>
    /// 统一向目标提交伤害请求的应用层契约。
    /// </summary>
    public interface IDamageRequestProcessor
    {
        /// <summary>
        /// 尝试向指定目标提交一次伤害请求。
        /// </summary>
        /// <param name="target">待受伤目标；无效或已死亡目标不会被处理。</param>
        /// <param name="request">要提交的只读伤害请求。</param>
        /// <param name="result">处理结果；失败时也会写入对应状态。</param>
        /// <returns>仅当伤害结果为已造成伤害或已击杀时返回 <c>true</c>。</returns>
        bool TryApplyDamage(IDamageable target, in DamageRequest request, out DamageResult result);
    }
}
