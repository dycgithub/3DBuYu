using Unity.Entities;
using Unity.Mathematics;

namespace GeometricMotion
{
    /// <summary>几何运动阶段，用于描述形变时序而不是移动所有权。</summary>
    public enum GeometricMotionPhase : byte
    {
        Stable,
        Prepare,
        Release,
        Impact,
        Rebound,
        Settle,
    }

    /// <summary>几何运动的离散表现信号。</summary>
    public enum GeometricMotionSignalType : byte
    {
        None,
        Prepare,
        Release,
        Impact,
        Rebound,
    }

    /// <summary>几何形变使用的局部前进轴。</summary>
    public enum GeometricMotionAxis : byte
    {
        X,
        Y,
        Z,
    }

    /// <summary>供 Burst 数学使用的固定缓动类型。</summary>
    public enum GeometricMotionEase : byte
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut,
        BackOut,
    }

    /// <summary>
    /// 几何运动的不可变配置快照。
    /// <para>ScriptableObject 配置必须先转换为此结构，ECS 才能在 Job 中安全读取。</para>
    /// </summary>
    public struct GeometricMotionConfig : IComponentData
    {
        /// <summary>达到最大速度伸展时使用的参考速度，单位为世界单位/秒。</summary>
        public float ReferenceSpeed;

        /// <summary>达到最大加速度形变时使用的参考加速度，单位为世界单位/秒²。</summary>
        public float ReferenceAcceleration;

        /// <summary>持续高速运动时沿前进轴的最大伸展比例。</summary>
        public float TravelStretch;

        /// <summary>加速度产生的最大额外前进轴伸展比例。</summary>
        public float AccelerationStretch;

        /// <summary>蓄力阶段沿前进轴的最大挤压比例。</summary>
        public float PrepareSquash;

        /// <summary>释放阶段沿前进轴的最大伸展比例。</summary>
        public float ReleaseStretch;

        /// <summary>撞击阶段沿前进轴的最大挤压比例。</summary>
        public float ImpactSquash;

        /// <summary>回弹阶段沿前进轴的最大伸展比例。</summary>
        public float ReboundStretch;

        /// <summary>惯性跟随的最大局部位移，单位为世界单位。</summary>
        public float FollowOffset;

        /// <summary>惯性跟随产生的最大局部旋转，单位为弧度。</summary>
        public float FollowTilt;

        /// <summary>惯性跟随的响应速度，单位为每秒。</summary>
        public float FollowResponse;

        /// <summary>Prepare 阶段时长，单位为秒。</summary>
        public float PrepareDuration;

        /// <summary>Release 阶段时长，单位为秒。</summary>
        public float ReleaseDuration;

        /// <summary>Impact 阶段时长，单位为秒。</summary>
        public float ImpactDuration;

        /// <summary>Rebound 阶段时长，单位为秒。</summary>
        public float ReboundDuration;

        /// <summary>Settle 阶段时长，单位为秒。</summary>
        public float SettleDuration;

        /// <summary>Prepare 阶段缓动。</summary>
        public GeometricMotionEase PrepareEase;

        /// <summary>Release 阶段缓动。</summary>
        public GeometricMotionEase ReleaseEase;

        /// <summary>Impact 阶段缓动。</summary>
        public GeometricMotionEase ImpactEase;

        /// <summary>Rebound 阶段缓动。</summary>
        public GeometricMotionEase ReboundEase;

        /// <summary>Settle 阶段缓动。</summary>
        public GeometricMotionEase SettleEase;

        /// <summary>形变使用的局部前进轴。</summary>
        public GeometricMotionAxis Axis;

        /// <summary>返回适合普通几何体的默认配置。</summary>
        public static GeometricMotionConfig Default => new()
        {
            ReferenceSpeed = 5f,
            ReferenceAcceleration = 10f,
            TravelStretch = 0.35f,
            AccelerationStretch = 0.15f,
            PrepareSquash = 0.25f,
            ReleaseStretch = 0.45f,
            ImpactSquash = 0.35f,
            ReboundStretch = 0.2f,
            FollowOffset = 0.08f,
            FollowTilt = 0.12f,
            FollowResponse = 10f,
            PrepareDuration = 0.12f,
            ReleaseDuration = 0.12f,
            ImpactDuration = 0.08f,
            ReboundDuration = 0.12f,
            SettleDuration = 0.18f,
            PrepareEase = GeometricMotionEase.EaseInOut,
            ReleaseEase = GeometricMotionEase.EaseOut,
            ImpactEase = GeometricMotionEase.EaseOut,
            ReboundEase = GeometricMotionEase.BackOut,
            SettleEase = GeometricMotionEase.EaseOut,
            Axis = GeometricMotionAxis.Z,
        };

        /// <summary>把外部配置限制到数学计算可以安全使用的范围。</summary>
        /// <returns>归一化后的配置副本。</returns>
        public GeometricMotionConfig Normalize()
        {
            GeometricMotionConfig normalized = this;
            normalized.ReferenceSpeed = math.max(0.001f, ReferenceSpeed);
            normalized.ReferenceAcceleration = math.max(0.001f, ReferenceAcceleration);
            normalized.TravelStretch = math.max(0f, TravelStretch);
            normalized.AccelerationStretch = math.max(0f, AccelerationStretch);
            normalized.PrepareSquash = math.clamp(PrepareSquash, 0f, 0.95f);
            normalized.ReleaseStretch = math.max(0f, ReleaseStretch);
            normalized.ImpactSquash = math.clamp(ImpactSquash, 0f, 0.95f);
            normalized.ReboundStretch = math.max(0f, ReboundStretch);
            normalized.FollowOffset = math.max(0f, FollowOffset);
            normalized.FollowTilt = math.max(0f, FollowTilt);
            normalized.FollowResponse = math.max(0f, FollowResponse);
            normalized.PrepareDuration = math.max(0f, PrepareDuration);
            normalized.ReleaseDuration = math.max(0f, ReleaseDuration);
            normalized.ImpactDuration = math.max(0f, ImpactDuration);
            normalized.ReboundDuration = math.max(0f, ReboundDuration);
            normalized.SettleDuration = math.max(0f, SettleDuration);
            return normalized;
        }
    }

    /// <summary>移动源在一个采样步提供的实际运动数据。</summary>
    public struct GeometricMotionInput : IComponentData
    {
        /// <summary>世界坐标速度，单位为世界单位/秒。</summary>
        public float3 Velocity;

        /// <summary>相对视觉实体局部坐标的加速度，单位为世界单位/秒²。</summary>
        public float3 LocalAcceleration;

        /// <summary>本次采样的时间步长，单位为秒。</summary>
        public float DeltaTime;
    }

    /// <summary>等待表现系统消费的一次离散运动信号。</summary>
    public struct GeometricMotionSignal : IComponentData
    {
        /// <summary>信号类型。</summary>
        public GeometricMotionSignalType Type;

        /// <summary>信号强度，范围为 0 到 1。</summary>
        public float Strength;

        /// <summary>产生信号的世界方向，供需要方向的适配器保留。</summary>
        public float3 Direction;
    }

    /// <summary>跨帧保存的几何运动阶段状态。</summary>
    public struct GeometricMotionState : IComponentData
    {
        /// <summary>当前表现阶段。</summary>
        public GeometricMotionPhase Phase;

        /// <summary>当前阶段已经经过的时间，单位为秒。</summary>
        public float PhaseTime;

        /// <summary>当前阶段使用的有符号前进轴形变值。</summary>
        public float PhaseValue;

        /// <summary>当前阶段的归一化强度。</summary>
        public float PhaseStrength;

        /// <summary>惯性跟随的局部位移。</summary>
        public float3 Follow;
    }

    /// <summary>保存源模型局部矩阵，避免每帧累计形变。</summary>
    public struct GeometricMotionBaseTransform : IComponentData
    {
        /// <summary>未叠加几何运动形变的模型局部矩阵。</summary>
        public float4x4 Value;
    }

    /// <summary>纯数学层输出的局部表现结果。</summary>
    public struct GeometricMotionOutput
    {
        /// <summary>非均匀局部缩放。</summary>
        public float3 Scale;

        /// <summary>惯性跟随产生的局部位移。</summary>
        public float3 LocalOffset;

        /// <summary>惯性跟随产生的局部旋转。</summary>
        public quaternion LocalRotation;
    }
}
