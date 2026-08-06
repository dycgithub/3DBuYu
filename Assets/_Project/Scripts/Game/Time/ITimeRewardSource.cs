namespace GameSystem
{
    /// <summary>
    /// 击杀时间奖励来源接口。
    /// 由具体实现决定击杀敌人后是否奖励额外时间（连击倍率、积分阈值转换等）。
    /// 注册到 ServiceLocator 供 GameManager 使用。
    /// </summary>
    public interface ITimeRewardSource
    {
        /// <summary>
        /// 计算击杀敌人应奖励的时间（秒）。
        /// 返回 0 表示无奖励。
        /// </summary>
        /// <param name="enemyPointsValue">敌人提供的积分值。</param>
        /// <param name="currentCombo">当前连击数（可选使用）。</param>
        float GetKillTimeReward(int enemyPointsValue, int currentCombo);
    }

    /// <summary>
    /// 默认空实现 — 不奖励时间。
    /// 击杀只给积分/货币，不加时间。
    /// 可被替换为有奖励的实现。
    /// </summary>
    public class NullTimeRewardSource : ITimeRewardSource
    {
        public float GetKillTimeReward(int enemyPointsValue, int currentCombo) => 0f;
    }
}
