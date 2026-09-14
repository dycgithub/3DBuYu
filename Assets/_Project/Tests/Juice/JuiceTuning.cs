using UnityEngine;

namespace _Project.Tests.Juice
{
    /// <summary>
    /// 将 Juice 控件的归一化值映射到测试场景本地表现参数。
    /// </summary>
    public static class JuiceTuning
    {
        /// <summary>
        /// 将 0 到 1 的滑块值映射为 scale punch 强度。
        /// </summary>
        /// <param name="sliderValue">UI 滑块的归一化值。</param>
        /// <param name="maximumPunch">测试场景允许的最大缩放脉冲。</param>
        /// <returns>限制在有效范围内的缩放脉冲强度。</returns>
        public static float MapScalePunch(float sliderValue, float maximumPunch)
        {
            return Mathf.Clamp01(sliderValue) * Mathf.Max(0f, maximumPunch);
        }
    }
}
