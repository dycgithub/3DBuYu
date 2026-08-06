namespace GameSystem
{
    /// <summary>
    /// 加时扩展点接口。
    /// 外部系统（道具、技能、事件）实现此接口即可在时间耗尽时提供额外时间。
    /// </summary>
    public interface ITimeExtension
    {
        /// <summary>
        /// 获取额外增加的时间（秒）。返回 0 表示不加时。
        /// </summary>
        float GetExtraTime();

        /// <summary>
        /// 获取优先级。值越小越先执行。
        /// </summary>
        int Priority { get; }
    }
}
