using System;
using UnityEngine;

namespace Services
{
    /// <summary>跨场景广播积分获得位置，供 UI 表现层订阅。</summary>
    public interface IPointsFlyEffectService
    {
        event Action<int, Vector3> PointsAwarded;

        /// <summary>广播一次积分获得事件。</summary>
        void Publish(int points, Vector3 worldPosition);
    }
}
