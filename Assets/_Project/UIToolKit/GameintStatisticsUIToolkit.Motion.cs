using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 提供 GameintStatisticsUIToolkit 的计数动画和视觉状态处理。
/// </summary>
public sealed partial class GameintStatisticsUIToolkit
{
    /// <summary>
    /// 推进面板入场动画，避免显示时突然出现在屏幕上。
    /// </summary>
    private void TickPanel(float deltaTime)
    {
        if (_panelEntryTime <= 0f)
        {
            return;
        }

        _panelEntryTime = Mathf.MoveTowards(
            _panelEntryTime,
            0f,
            deltaTime);
        float progress = 1f - _panelEntryTime / PanelEntryDuration;
        float easedProgress = Ease(progress);
        _panel.style.scale = new Scale(
            Vector3.one * Mathf.Lerp(0.88f, 1f, easedProgress));
        _panel.style.opacity = easedProgress;
    }

    /// <summary>
    /// 推进单个数字的逐点变化、数量级缩放、弹跳和颜色提亮反馈。
    /// </summary>
    private static bool TickCounter(
        ref CounterMotion motion,
        Label valueLabel,
        VisualElement card,
        float punchScale,
        float deltaTime)
    {
        bool stepped = false;
        if (motion.Value != motion.Target)
        {
            motion.StepTimer += deltaTime;
            int steps = 0;
            while (motion.Value != motion.Target && steps < MaxStepsPerFrame)
            {
                float interval = GetStepInterval(
                    Mathf.Abs(motion.Target - motion.Value));
                if (motion.StepTimer < interval)
                {
                    break;
                }

                motion.StepTimer -= interval;
                bool rising = motion.Target > motion.Value;
                motion.Value += rising ? 1 : -1;
                motion.PunchTime = Mathf.Max(motion.PunchTime, StepPunchDuration);
                motion.PunchDuration = Mathf.Max(
                    motion.PunchDuration,
                    StepPunchDuration);
                motion.FlashTime = ImpactFlashDuration;
                if (rising)
                {
                    motion.LiftTime = StepLiftDuration;
                }

                stepped = true;
                steps++;
            }
        }

        bool active = motion.Value != motion.Target ||
                      motion.PunchTime > 0f ||
                      motion.FlashTime > 0f ||
                      motion.LiftTime > 0f ||
                      motion.EntryTime > 0f;
        if (!active)
        {
            return false;
        }

        motion.PunchTime = Mathf.MoveTowards(
            motion.PunchTime,
            0f,
            deltaTime);
        motion.FlashTime = Mathf.MoveTowards(motion.FlashTime, 0f, deltaTime);
        motion.LiftTime = Mathf.MoveTowards(motion.LiftTime, 0f, deltaTime);
        motion.EntryTime = Mathf.MoveTowards(motion.EntryTime, 0f, deltaTime);

        RenderCounter(ref motion, valueLabel);
        float pulse = GetPulse(motion, punchScale);
        float lift = GetLift(motion);
        float entryProgress = 1f - motion.EntryTime / CardEntryDuration;
        float cardScale = Ease(entryProgress);
        card.style.scale = new Scale(Vector3.one * cardScale);
        valueLabel.style.scale = new Scale(
            Vector3.one * (GetMagnitudeScale(motion.Value) * pulse));
        valueLabel.style.translate = new Vector3(0f, -lift, 0f);
        valueLabel.EnableInClassList(ImpactClass, motion.FlashTime > 0f);
        return stepped;
    }

    /// <summary>
    /// 根据待追步数计算每一步的间隔；大跨度只缩短间隔，不跳过数字。
    /// </summary>
    private static float GetStepInterval(int pendingSteps)
    {
        if (pendingSteps <= FastStepThreshold)
        {
            return StepInterval;
        }

        float compression = FastStepThreshold / (float)pendingSteps;
        return Mathf.Max(FastestStepInterval, StepInterval * compression);
    }

    /// <summary>
    /// 只在整数值变化时刷新文本，并保留纯数字显示。
    /// </summary>
    private static void RenderCounter(ref CounterMotion motion, Label valueLabel)
    {
        if (motion.RenderedValue == motion.Value)
        {
            return;
        }

        valueLabel.text = motion.Value.ToString("N0");
        motion.RenderedValue = motion.Value;
    }

    /// <summary>
    /// 根据数值数量级计算有限的基础缩放，避免大数字无限扩大。
    /// </summary>
    private static float GetMagnitudeScale(int value)
    {
        float magnitude = Mathf.Log10(Mathf.Max(1f, (float)value + 1f));
        return 1f + Mathf.Min(
            MaximumMagnitudeScale - 1f,
            magnitude * MagnitudeScalePerDigit);
    }

    /// <summary>计算数字冲击的缩放曲线。</summary>
    private static float GetPulse(CounterMotion motion, float punchScale)
    {
        if (motion.PunchTime <= 0f || motion.PunchDuration <= 0f)
        {
            return 1f;
        }

        float progress = 1f - motion.PunchTime / motion.PunchDuration;
        return 1f + Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * punchScale;
    }

    /// <summary>计算逐点增加时的短暂上移距离。</summary>
    private static float GetLift(CounterMotion motion)
    {
        if (motion.LiftTime <= 0f)
        {
            return 0f;
        }

        float progress = 1f - motion.LiftTime / StepLiftDuration;
        return Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * StepLift;
    }

    /// <summary>使用平滑步缓动，避免入场和数字冲击出现机械线性运动。</summary>
    private static float Ease(float progress)
    {
        float normalized = Mathf.Clamp01(progress);
        return normalized * normalized * (3f - 2f * normalized);
    }

    /// <summary>
    /// 隐藏面板和卡片，并把未完成的动画静默收敛到目标值。
    /// </summary>
    private void HidePanel()
    {
        if (_panel == null)
        {
            return;
        }

        SetImmediate(ref _pointsMotion, _pointsMotion.Target);
        SetImmediate(ref _streakMotion, _streakMotion.Target);
        SetImmediate(ref _killsMotion, _killsMotion.Target);
        RenderCounter(ref _pointsMotion, _pointsLabel);
        RenderCounter(ref _streakMotion, _streakLabel);
        RenderCounter(ref _killsMotion, _killsLabel);
        ResetVisual(_pointsLabel, _pointsCard);
        ResetVisual(_streakLabel, _streakCard);
        ResetVisual(_killsLabel, _killsCard);

        _hideTimer = 0f;
        _panelEntryTime = 0f;
        _panel.EnableInClassList(HiddenClass, true);
        _panel.style.scale = new Scale(Vector3.one);
        _panel.style.opacity = 1f;
    }

    /// <summary>隐藏卡片并清除数字的颜色反馈和动态变换。</summary>
    private static void ResetVisual(Label valueLabel, VisualElement card)
    {
        card.EnableInClassList(HiddenClass, true);
        valueLabel.EnableInClassList(ImpactClass, false);
        card.style.scale = new Scale(Vector3.one);
        valueLabel.style.scale = new Scale(Vector3.one);
        valueLabel.style.translate = Vector3.zero;
    }

    /// <summary>判断当前是否仍有数字或面板入场动画未完成。</summary>
    private bool HasActiveMotion()
    {
        return _panelEntryTime > 0f ||
               HasActiveMotion(_pointsMotion) ||
               HasActiveMotion(_streakMotion) ||
               HasActiveMotion(_killsMotion);
    }

    /// <summary>判断单个计数器是否仍在追数或播放反馈。</summary>
    private static bool HasActiveMotion(CounterMotion motion)
    {
        return motion.Value != motion.Target ||
               motion.PunchTime > 0f ||
               motion.FlashTime > 0f ||
               motion.LiftTime > 0f ||
               motion.EntryTime > 0f;
    }

    /// <summary>判断时间值是否可以安全参与动画计算。</summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
