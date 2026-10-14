namespace CombatSystem
{
    /// <summary>
    /// 直接提交投射物发射请求的边界接口。
    /// </summary>
    public interface IProjectileLauncher
    {
        /// <summary>
        /// 生成请求指定的投射物，不检查能量、冷却或战斗阶段。
        /// </summary>
        /// <param name="request">已经准备好的投射物请求。</param>
        /// <returns>投射物生成请求被接受时返回 <c>true</c>。</returns>
        bool Launch(in ProjectileLaunchRequest request);

        /// <summary>
        /// 提交已经构建完成的攻击数据，不重复处理资源或冷却。
        /// </summary>
        /// <param name="attackData">已经包含最终攻击参数的数据。</param>
        /// <returns>投射物生成请求被接受时返回 <c>true</c>。</returns>
        bool Launch(in AttackData attackData);
    }
}
