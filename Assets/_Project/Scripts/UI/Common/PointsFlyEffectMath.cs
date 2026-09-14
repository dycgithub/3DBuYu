using UnityEngine;

namespace _Project.UI.Common
{
    /// <summary>
    /// 计算积分提示从敌人位置飞向积分栏的世界坐标轨迹。
    /// </summary>
    public static class PointsFlyEffectMath
    {
        /// <summary>
        /// 在起点和终点之间插值，并在中段加入抛物线高度。
        /// </summary>
        public static Vector3 CalculatePosition(
            Vector3 start,
            Vector3 target,
            float progress,
            float arcHeight)
        {
            float normalizedProgress = Mathf.Clamp01(progress);
            Vector3 linearPosition = Vector3.LerpUnclamped(start, target, normalizedProgress);
            float arcProgress = 4f * normalizedProgress * (1f - normalizedProgress);
            return linearPosition + Vector3.up * (Mathf.Max(0f, arcHeight) * arcProgress);
        }
    }
}
