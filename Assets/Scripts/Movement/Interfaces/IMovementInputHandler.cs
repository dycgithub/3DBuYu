using UnityEngine;

namespace SphereMovement.Interfaces
{
    /// <summary>
    /// 移动输入处理器接口
    /// </summary>
    public interface IMovementInputHandler
    {
        /// <summary>
        /// 移动速度（度/秒）
        /// </summary>
        float MoveSpeed { get; set; }

        /// <summary>
        /// 处理输入并返回球坐标变化量
        /// </summary>
        /// <param name="deltaTime">时间增量</param>
        /// <returns>球坐标变化 (x=经度变化, y=纬度变化)</returns>
        Vector2 ProcessInput(float deltaTime);

        /// <summary>
        /// 是否有活动输入
        /// </summary>
        bool HasActiveInput { get; }
    }
}
