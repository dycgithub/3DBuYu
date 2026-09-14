using System;
using UnityEngine;

namespace Services
{
    /// <summary>由根容器持有的跨场景积分表现事件服务。</summary>
    public sealed class PointsFlyEffectService : IPointsFlyEffectService
    {
        public event Action<int, Vector3> PointsAwarded;

        /// <summary>发布积分数值和产生积分的世界坐标。</summary>
        public void Publish(int points, Vector3 worldPosition)
        {
            PointsAwarded?.Invoke(points, worldPosition);
        }
    }
}
