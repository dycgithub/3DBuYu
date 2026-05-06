namespace SphereMovement.Interfaces
{
    /// <summary>
    /// 输入提供接口，用于解耦输入源
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// 水平输入 (-1 到 1)
        /// </summary>
        float Horizontal { get; }

        /// <summary>
        /// 垂直输入 (-1 到 1)
        /// </summary>
        float Vertical { get; }

        /// <summary>
        /// 是否有输入
        /// </summary>
        bool HasInput { get; }
    }
}
