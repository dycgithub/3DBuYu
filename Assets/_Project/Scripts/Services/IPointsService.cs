using System;

namespace Services
{
    /// <summary>
    /// 积分服务接口（统一货币）。
    /// 替代原先的 ICurrencyService（金币+宝石双货币），
    /// 所有经济操作统一使用积分（Points）。
    /// </summary>
    public interface IPointsService
    {
        /// <summary>当前积分数。</summary>
        int Points { get; }

        /// <summary>是否有足够积分。</summary>
        bool HasEnoughPoints(int amount);

        /// <summary>消费积分。成功返回 true。</summary>
        bool SpendPoints(int amount, string reason);

        /// <summary>增加积分。</summary>
        void AddPoints(int amount, string source);

        /// <summary>积分变化事件（当前值, 变化量）。</summary>
        event Action<int, int> OnPointsChanged;
    }
}