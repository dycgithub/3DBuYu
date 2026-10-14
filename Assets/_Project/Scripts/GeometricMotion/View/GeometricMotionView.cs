using UnityEngine;

namespace GeometricMotion
{
    /// <summary>
    /// 普通 GameObject 的几何运动表现适配器。
    /// <para>组件只修改视觉节点的局部表现，不接管移动根节点的位置和旋转。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeometricMotionView : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GeometricMotionProfileSO profile;
        [SerializeField] private Transform visualRoot;

        private GeometricMotionConfig _config;
        private GeometricMotionState _state;
        private GeometricMotionSignal _pendingSignal;
        private GeometricMotionInput _explicitInput;
        private Vector3 _baseScale;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private bool _initialized;
        private Vector3 _lastPosition;
        private Vector3 _lastVelocity;
        private bool _hasSample;
        private bool _hasExplicitInput;

        /// <summary>缓存视觉节点和未形变的局部姿态。</summary>
        private void Awake()
        {
            Initialize();
        }

        /// <summary>对象启用时清理阶段和外部采样，适配对象池复用。</summary>
        private void OnEnable()
        {
            if (!_initialized)
            {
                Initialize();
            }

            ResetMotion();
        }

        /// <summary>在移动者完成本帧移动后推进几何表现。</summary>
        private void LateUpdate()
        {
            Transform motionRoot = transform;
            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            if (!_hasSample)
            {
                _lastPosition = motionRoot.position;
                _lastVelocity = Vector3.zero;
                _hasSample = true;
            }

            GeometricMotionInput input = _hasExplicitInput
                ? _explicitInput
                : CreateSample(motionRoot, deltaTime);
            input.DeltaTime = deltaTime;
            GeometricMotionOutput output = GeometricMotionMath.Evaluate(
                ref _state,
                _config,
                input,
                _pendingSignal);
            _pendingSignal = default;
            _hasExplicitInput = false;
            ApplyOutput(output);

            _lastPosition = motionRoot.position;
            _lastVelocity = input.Velocity;
        }

        /// <summary>
        /// 提交一帧外部运动采样，适用于移动者不直接暴露 Transform 位移的对象。
        /// </summary>
        /// <param name="velocity">世界坐标速度，单位为世界单位/秒。</param>
        /// <param name="localAcceleration">视觉根节点局部坐标加速度。</param>
        public void SetMotionInput(Vector3 velocity, Vector3 localAcceleration)
        {
            _explicitInput = new GeometricMotionInput
            {
                Velocity = new Unity.Mathematics.float3(velocity.x, velocity.y, velocity.z),
                LocalAcceleration = new Unity.Mathematics.float3(
                    localAcceleration.x,
                    localAcceleration.y,
                    localAcceleration.z),
            };
            _hasExplicitInput = true;
        }

        /// <summary>排队一个 Prepare、Release、Impact 或 Rebound 信号。</summary>
        /// <param name="type">要触发的离散表现阶段。</param>
        /// <param name="strength">信号强度，范围为 0 到 1。</param>
        /// <param name="direction">产生信号的世界方向。</param>
        public void Signal(
            GeometricMotionSignalType type,
            float strength = 1f,
            Vector3 direction = default)
        {
            _pendingSignal = GeometricMotionMath.MergeSignals(
                _pendingSignal,
                new GeometricMotionSignal
                {
                    Type = type,
                    Strength = strength,
                    Direction = new Unity.Mathematics.float3(
                        direction.x,
                        direction.y,
                        direction.z),
                });
        }

        /// <summary>清除阶段、跟随量、待处理信号并恢复基础局部姿态。</summary>
        public void ResetMotion()
        {
            _state = default;
            _pendingSignal = default;
            _explicitInput = default;
            _hasExplicitInput = false;
            _hasSample = false;
            ApplyOutput(new GeometricMotionOutput
            {
                Scale = new Unity.Mathematics.float3(1f),
                LocalRotation = Unity.Mathematics.quaternion.identity,
            });
        }

        /// <summary>缓存配置和视觉节点的初始局部姿态。</summary>
        private void Initialize()
        {
            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            _config = profile != null
                ? profile.CreateConfig()
                : GeometricMotionConfig.Default;
            if (_initialized)
            {
                return;
            }

            _baseScale = visualRoot.localScale;
            _basePosition = visualRoot.localPosition;
            _baseRotation = visualRoot.localRotation;
            _initialized = true;
        }

        /// <summary>从移动根节点位置变化计算本帧运动输入。</summary>
        private GeometricMotionInput CreateSample(Transform motionRoot, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return default;
            }

            Vector3 velocity = (motionRoot.position - _lastPosition) / deltaTime;
            Vector3 acceleration = (velocity - _lastVelocity) / deltaTime;
            Vector3 localAcceleration = motionRoot.InverseTransformDirection(acceleration);
            return new GeometricMotionInput
            {
                Velocity = new Unity.Mathematics.float3(velocity.x, velocity.y, velocity.z),
                LocalAcceleration = new Unity.Mathematics.float3(
                    localAcceleration.x,
                    localAcceleration.y,
                    localAcceleration.z),
            };
        }

        /// <summary>把数学结果应用到视觉节点，不改变移动根节点位置。</summary>
        private void ApplyOutput(GeometricMotionOutput output)
        {
            visualRoot.localScale = Vector3.Scale(
                _baseScale,
                new Vector3(output.Scale.x, output.Scale.y, output.Scale.z));
            visualRoot.localRotation = _baseRotation * new Quaternion(
                output.LocalRotation.value.x,
                output.LocalRotation.value.y,
                output.LocalRotation.value.z,
                output.LocalRotation.value.w);
            if (visualRoot != transform)
            {
                visualRoot.localPosition = _basePosition + new Vector3(
                    output.LocalOffset.x,
                    output.LocalOffset.y,
                    output.LocalOffset.z);
            }
        }
    }
}
