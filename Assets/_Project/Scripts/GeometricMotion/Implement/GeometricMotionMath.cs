using Unity.Mathematics;

namespace GeometricMotion
{
    /// <summary>
    /// 计算几何运动表现的无状态数学工具。
    /// <para>该类型不拥有 Transform、ECS 实体或 Tween，ECS 与 GameObject 适配器共享同一套结果。</para>
    /// </summary>
    public static class GeometricMotionMath
    {
        private const float MinimumScale = 0.05f;
        private const float MinimumDuration = 0.0001f;
        private const int MaximumPhaseTransitions = 8;

        /// <summary>
        /// 推进一个几何运动状态，并返回当前帧应使用的局部表现。
        /// </summary>
        /// <param name="state">要读写的跨帧表现状态。</param>
        /// <param name="rawConfig">尚未归一化的表现配置。</param>
        /// <param name="input">当前运动源提供的速度和加速度。</param>
        /// <param name="signal">当前帧待消费的离散动作信号。</param>
        /// <returns>当前帧的非均匀缩放、跟随位移和局部旋转。</returns>
        public static GeometricMotionOutput Evaluate(
            ref GeometricMotionState state,
            in GeometricMotionConfig rawConfig,
            in GeometricMotionInput input,
            in GeometricMotionSignal signal)
        {
            GeometricMotionConfig config = rawConfig.Normalize();
            float deltaTime = math.max(0f, input.DeltaTime);
            if (signal.Type != GeometricMotionSignalType.None && signal.Strength > 0f)
            {
                StartSignal(ref state, signal, config);
            }

            float3 velocity = Sanitize(input.Velocity);
            float3 localAcceleration = Sanitize(input.LocalAcceleration);
            UpdateFollow(ref state, localAcceleration, deltaTime, config);

            GeometricMotionState visualState = state;
            GetVisualPhase(ref visualState, deltaTime, config);
            float phaseOffset = GetPhaseOffset(visualState, config);
            float speed01 = math.saturate(math.length(velocity) / config.ReferenceSpeed);
            float acceleration01 = math.saturate(
                math.length(localAcceleration) / config.ReferenceAcceleration);
            float axisScale = math.max(
                MinimumScale,
                1f
                    + speed01 * config.TravelStretch
                    + acceleration01 * config.AccelerationStretch
                    + phaseOffset);
            float lateralScale = math.rsqrt(axisScale);

            GeometricMotionOutput output = new()
            {
                Scale = CreateScale(config.Axis, axisScale, lateralScale),
                LocalOffset = state.Follow,
                LocalRotation = CreateFollowRotation(state.Follow, config.FollowTilt),
            };

            AdvancePhase(ref state, deltaTime, config);
            return output;
        }

        /// <summary>合并同一实体同一帧收到的多个信号。</summary>
        /// <param name="current">当前已经排队的信号。</param>
        /// <param name="incoming">新收到的信号。</param>
        /// <returns>按优先级和强度选择的信号。</returns>
        public static GeometricMotionSignal MergeSignals(
            GeometricMotionSignal current,
            GeometricMotionSignal incoming)
        {
            incoming.Strength = math.saturate(incoming.Strength);
            if (incoming.Type == GeometricMotionSignalType.None || incoming.Strength <= 0f)
            {
                return current;
            }

            current.Strength = math.saturate(current.Strength);
            if (current.Type == GeometricMotionSignalType.None ||
                SignalPriority(incoming.Type) > SignalPriority(current.Type) ||
                SignalPriority(incoming.Type) == SignalPriority(current.Type) &&
                incoming.Strength > current.Strength)
            {
                return incoming;
            }

            return current;
        }

        /// <summary>开始一个离散表现阶段。</summary>
        private static void StartSignal(
            ref GeometricMotionState state,
            GeometricMotionSignal signal,
            GeometricMotionConfig config)
        {
            float strength = math.saturate(signal.Strength);
            state.PhaseTime = 0f;
            state.PhaseStrength = strength;
            state.Phase = signal.Type switch
            {
                GeometricMotionSignalType.Prepare => GeometricMotionPhase.Prepare,
                GeometricMotionSignalType.Release => GeometricMotionPhase.Release,
                GeometricMotionSignalType.Impact => GeometricMotionPhase.Impact,
                GeometricMotionSignalType.Rebound => GeometricMotionPhase.Rebound,
                _ => GeometricMotionPhase.Stable,
            };
            state.PhaseValue = GetSignalValue(signal.Type, strength, config);
        }

        /// <summary>根据动作信号读取有符号的前进轴形变值。</summary>
        private static float GetSignalValue(
            GeometricMotionSignalType signalType,
            float strength,
            GeometricMotionConfig config)
        {
            return signalType switch
            {
                GeometricMotionSignalType.Prepare => -config.PrepareSquash * strength,
                GeometricMotionSignalType.Release => config.ReleaseStretch * strength,
                GeometricMotionSignalType.Impact => -config.ImpactSquash * strength,
                GeometricMotionSignalType.Rebound => config.ReboundStretch * strength,
                _ => 0f,
            };
        }

        /// <summary>把跟随位移平滑到当前加速度方向，形成惯性和次要动作。</summary>
        private static void UpdateFollow(
            ref GeometricMotionState state,
            float3 localAcceleration,
            float deltaTime,
            GeometricMotionConfig config)
        {
            float acceleration01 = math.saturate(
                math.length(localAcceleration) / config.ReferenceAcceleration);
            float3 target = math.normalizesafe(-localAcceleration, float3.zero)
                * (config.FollowOffset * acceleration01);
            float blend = 1f - math.exp(-config.FollowResponse * deltaTime);
            state.Follow = math.lerp(state.Follow, target, math.saturate(blend));
        }

        /// <summary>读取经过当前步长后的视觉阶段，不改变真实状态。</summary>
        private static void GetVisualPhase(
            ref GeometricMotionState state,
            float deltaTime,
            GeometricMotionConfig config)
        {
            state.PhaseTime += deltaTime;
            int transitionCount = 0;
            while (transitionCount < MaximumPhaseTransitions)
            {
                if (state.Phase == GeometricMotionPhase.Prepare)
                {
                    state.PhaseTime = math.min(
                        state.PhaseTime,
                        GetDuration(state.Phase, config));
                    return;
                }

                float duration = GetDuration(state.Phase, config);
                if (duration > MinimumDuration && state.PhaseTime <= duration)
                {
                    return;
                }

                float overflow = duration > MinimumDuration
                    ? state.PhaseTime - duration
                    : state.PhaseTime;
                if (!TryTransition(ref state, config))
                {
                    state.PhaseTime = 0f;
                    return;
                }

                state.PhaseTime = math.max(0f, overflow);
                transitionCount++;
            }
        }

        /// <summary>推进真实阶段状态，并处理一个步长内的连续阶段切换。</summary>
        private static void AdvancePhase(
            ref GeometricMotionState state,
            float deltaTime,
            GeometricMotionConfig config)
        {
            if (deltaTime <= 0f || state.Phase == GeometricMotionPhase.Stable)
            {
                return;
            }

            state.PhaseTime += deltaTime;
            if (state.Phase == GeometricMotionPhase.Prepare)
            {
                state.PhaseTime = math.min(
                    state.PhaseTime,
                    GetDuration(state.Phase, config));
                return;
            }
            int transitionCount = 0;
            while (transitionCount < MaximumPhaseTransitions)
            {
                float duration = GetDuration(state.Phase, config);
                if (duration > MinimumDuration && state.PhaseTime < duration)
                {
                    return;
                }

                float overflow = duration > MinimumDuration
                    ? state.PhaseTime - duration
                    : state.PhaseTime;
                if (!TryTransition(ref state, config))
                {
                    state.PhaseTime = 0f;
                    return;
                }

                state.PhaseTime = math.max(0f, overflow);
                transitionCount++;
                if (state.Phase == GeometricMotionPhase.Stable)
                {
                    state.PhaseTime = 0f;
                    return;
                }
            }
        }

        /// <summary>按当前阶段生成下一个阶段，返回是否发生切换。</summary>
        private static bool TryTransition(
            ref GeometricMotionState state,
            GeometricMotionConfig config)
        {
            switch (state.Phase)
            {
                case GeometricMotionPhase.Release:
                    state.Phase = GeometricMotionPhase.Settle;
                    return true;
                case GeometricMotionPhase.Impact:
                    state.Phase = GeometricMotionPhase.Rebound;
                    state.PhaseValue = config.ReboundStretch * state.PhaseStrength;
                    return true;
                case GeometricMotionPhase.Rebound:
                    state.Phase = GeometricMotionPhase.Settle;
                    return true;
                case GeometricMotionPhase.Settle:
                    state.Phase = GeometricMotionPhase.Stable;
                    state.PhaseValue = 0f;
                    state.PhaseStrength = 0f;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>计算视觉阶段的有符号前进轴偏移。</summary>
        private static float GetPhaseOffset(
            GeometricMotionState state,
            GeometricMotionConfig config)
        {
            float duration = GetDuration(state.Phase, config);
            float progress = duration <= MinimumDuration
                ? 1f
                : math.saturate(state.PhaseTime / duration);
            float eased = ApplyEase(GetEase(state.Phase, config), progress);
            return state.Phase == GeometricMotionPhase.Settle
                ? state.PhaseValue * (1f - eased)
                : state.PhaseValue * eased;
        }

        /// <summary>读取阶段时长。</summary>
        private static float GetDuration(
            GeometricMotionPhase phase,
            GeometricMotionConfig config)
        {
            return phase switch
            {
                GeometricMotionPhase.Prepare => config.PrepareDuration,
                GeometricMotionPhase.Release => config.ReleaseDuration,
                GeometricMotionPhase.Impact => config.ImpactDuration,
                GeometricMotionPhase.Rebound => config.ReboundDuration,
                GeometricMotionPhase.Settle => config.SettleDuration,
                _ => 0f,
            };
        }

        /// <summary>读取阶段缓动。</summary>
        private static GeometricMotionEase GetEase(
            GeometricMotionPhase phase,
            GeometricMotionConfig config)
        {
            return phase switch
            {
                GeometricMotionPhase.Prepare => config.PrepareEase,
                GeometricMotionPhase.Release => config.ReleaseEase,
                GeometricMotionPhase.Impact => config.ImpactEase,
                GeometricMotionPhase.Rebound => config.ReboundEase,
                GeometricMotionPhase.Settle => config.SettleEase,
                _ => GeometricMotionEase.Linear,
            };
        }

        /// <summary>应用不依赖 Unity AnimationCurve 的固定缓动。</summary>
        private static float ApplyEase(GeometricMotionEase ease, float progress)
        {
            float t = math.saturate(progress);
            return ease switch
            {
                GeometricMotionEase.EaseIn => t * t,
                GeometricMotionEase.EaseOut => 1f - (1f - t) * (1f - t),
                GeometricMotionEase.EaseInOut => t < 0.5f
                    ? 2f * t * t
                    : 1f - math.pow(-2f * t + 2f, 2f) * 0.5f,
                GeometricMotionEase.BackOut => ApplyBackOut(t),
                _ => t,
            };
        }

        /// <summary>应用带少量回弹的出场缓动。</summary>
        private static float ApplyBackOut(float progress)
        {
            const float overshoot = 1.70158f;
            float value = progress - 1f;
            return 1f + (overshoot + 1f) * value * value * value + overshoot * value * value;
        }

        /// <summary>按前进轴构造体积近似保持的缩放向量。</summary>
        private static float3 CreateScale(
            GeometricMotionAxis axis,
            float axisScale,
            float lateralScale)
        {
            return axis switch
            {
                GeometricMotionAxis.X => new float3(axisScale, lateralScale, lateralScale),
                GeometricMotionAxis.Y => new float3(lateralScale, axisScale, lateralScale),
                _ => new float3(lateralScale, lateralScale, axisScale),
            };
        }

        /// <summary>把跟随位移转换为小幅局部倾斜。</summary>
        private static quaternion CreateFollowRotation(float3 follow, float tilt)
        {
            float3 euler = new float3(-follow.y, follow.x, 0f) * tilt;
            return quaternion.EulerXYZ(euler);
        }

        /// <summary>把非法运动采样归零，避免传播 NaN。</summary>
        private static float3 Sanitize(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }

        /// <summary>读取离散信号优先级。</summary>
        private static int SignalPriority(GeometricMotionSignalType type)
        {
            return type switch
            {
                GeometricMotionSignalType.Prepare => 1,
                GeometricMotionSignalType.Release => 2,
                GeometricMotionSignalType.Rebound => 3,
                GeometricMotionSignalType.Impact => 4,
                _ => 0,
            };
        }
    }
}
