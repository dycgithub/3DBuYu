using R3;

namespace Services
{
    /// <summary>
    /// 积分服务接口(统一货币)。
    /// 当前积分以 R3 ReadOnlyReactiveProperty 暴露:订阅即得当前值,变化实时推送,
    /// 可直接对接项目 MVVM 绑定(如 BindingExtensions.BindText)。
    /// </summary>
    public interface IPointsService
    {
        /// <summary>当前积分(可观察,订阅时立即推送当前值,之后每次变化推送)。</summary>
        ReadOnlyReactiveProperty<int> Points { get; }

        /// <summary>是否有足够积分。</summary>
        bool HasEnoughPoints(int amount);

        /// <summary>消费积分。成功返回 true。</summary>
        bool SpendPoints(int amount, string reason);

        /// <summary>增加积分。</summary>
        void AddPoints(int amount, string source);
    }
}
