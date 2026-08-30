using R3;

namespace Services
{
    /// <summary>
    /// 提供本局连续击杀状态。
    /// 应用层负责写入击杀和推进计时，表现层只订阅 R3 状态。
    /// </summary>
    public interface IKillStreakService
    {
        /// <summary>当前未断连的击杀数。</summary>
        ReadOnlyReactiveProperty<int> CurrentStreak { get; }

        /// <summary>本局达到过的最高连续击杀数。</summary>
        ReadOnlyReactiveProperty<int> BestStreak { get; }

        /// <summary>
        /// 开始一局新的连杀记录，并清除上一局数据。
        /// </summary>
        /// <param name="windowSeconds">两次击杀允许间隔的秒数；非正数表示下一次计时推进时断连。</param>
        void BeginRun(float windowSeconds);

        /// <summary>
        /// 记录一次有效击杀。
        /// </summary>
        /// <returns>记录后的当前连杀数；服务未开始本局时返回 0。</returns>
        int RegisterKill();

        /// <summary>
        /// 推进连杀窗口计时；暂停状态下由调用方停止调用。
        /// </summary>
        /// <param name="deltaTime">经过的秒数；非有限或负数会被忽略。</param>
        void Tick(float deltaTime);
    }
}
