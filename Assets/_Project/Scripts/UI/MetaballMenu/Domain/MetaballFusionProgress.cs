using UnityEngine;

namespace _Project.UI.MetaballMenu
{
    /// <summary>
    /// 单个融球入口的运行时进度。
    /// 进度只接受已支付能量的帧；释放按键会清零进度，但不会处理能量返还。
    /// </summary>
    public sealed class MetaballFusionProgress
    {
        private readonly float _duration;

        public float Value { get; private set; }
        public bool IsComplete => Value >= 1f;
        public bool RequiresRelease { get; private set; }

        public MetaballFusionProgress(float duration)
        {
            _duration = NormalizeDuration(duration);
        }

        /// <summary>
        /// 推进一帧融合状态。
        /// </summary>
        /// <param name="deltaTime">经过的秒数。</param>
        /// <param name="isHeld">当前按键是否仍被按住。</param>
        /// <param name="paymentSucceeded">本帧能量是否支付成功。</param>
        /// <returns>本次调用是否首次完成融合。</returns>
        public bool Advance(float deltaTime, bool isHeld, bool paymentSucceeded)
        {
            if (!isHeld)
            {
                Reset();
                return false;
            }

            if (RequiresRelease || IsComplete || !paymentSucceeded)
                return false;

            if (!float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime) && deltaTime > 0f)
                Value = Mathf.Clamp01(Value + deltaTime / _duration);

            if (!IsComplete)
                return false;

            RequiresRelease = true;
            return true;
        }

        /// <summary>清零进度并允许下一次按住。</summary>
        public void Reset()
        {
            Value = 0f;
            RequiresRelease = false;
        }

        /// <summary>
        /// 清零进度，但要求先释放按键，避免面板关闭后因按键未释放而立即再次打开。
        /// </summary>
        public void RequireRelease()
        {
            Value = 0f;
            RequiresRelease = true;
        }

        private static float NormalizeDuration(float duration)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration))
                return 0.1f;

            return Mathf.Max(0.1f, duration);
        }
    }
}
