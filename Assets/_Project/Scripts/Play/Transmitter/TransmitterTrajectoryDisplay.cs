using CombatSystem;
using Interfaces;
using Services;
using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>
    /// 显示单个端口的轨迹预测。
    /// <para>子弹配置和基础射程来自绑定端口的 <see cref="TransmitterSO"/>。</para>
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TransmitterTrajectoryDisplay : MonoBehaviour
    {
        [Header("开关")]
        [SerializeField] private bool showTrajectory = true;

        [Header("外观")]
        [SerializeField] private Color trajectoryColor = new Color(0f, 1f, 1f, 0.6f);
        [SerializeField, Range(0.01f, 1f)] private float lineWidth = 0.1f;
        [SerializeField] private Material lineMaterial;

        [Header("采样")]
        [SerializeField, Range(10, 200)] private int predictionSteps = 60;
        [SerializeField, Range(0.01f, 0.2f)] private float timeStep = 0.05f;

        [SerializeField, Min(-1)] private int portIndex = -1;

        private LineRenderer _lineRenderer;
        private CentralCore _centralCore;
        private PhysicsCentralDetector _detector;
        private CentralTransmitterRuntime _port;
        private Transform _firePoint;
        private Vector3[] _points;
        private bool _initialized;

        [Inject] private IProjectileTrajectorySimulationService _trajectorySimulation;

        /// <summary>获取当前轨迹显示绑定的端口索引。</summary>
        public int PortIndex => portIndex;

        /// <summary>缓存 LineRenderer 并初始化其渲染模式。</summary>
        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 0;
            if (lineMaterial != null)
            {
                _lineRenderer.material = lineMaterial;
            }
        }

        /// <summary>
        /// 由 CentralCore 绑定所属端口，避免显示组件在每帧反查父级和端口集合。
        /// </summary>
        /// <param name="core">所属中央核心。</param>
        /// <param name="detector">提供端口目标的探测器。</param>
        /// <param name="port">当前显示对应的发射器端口。</param>
        public void Initialize(
            CentralCore core,
            PhysicsCentralDetector detector,
            CentralTransmitterRuntime port)
        {
            _centralCore = core;
            _detector = detector;
            _port = port;

            if (_port != null)
            {
                portIndex = _port.PortIndex;
                _firePoint = _port.FirePoint;
            }

            _initialized = _centralCore != null && _detector != null && _port != null;
        }

        /// <summary>按配置启用显示，并兼容未被中央核心主动绑定的旧预制体。</summary>
        private void Start()
        {
            if (!showTrajectory)
            {
                enabled = false;
                return;
            }

            TryInitializeFromHierarchy();
        }

        /// <summary>每帧重新计算当前端口轨迹并写入 LineRenderer。</summary>
        private void LateUpdate()
        {
            if (!_initialized)
            {
                TryInitializeFromHierarchy();
            }

            ProjectileProfileSO profile = _port?.So?.defaultProjectileProfile;
            if (!_initialized || _firePoint == null || _trajectorySimulation == null || profile == null)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            IDamageable target = _detector.GetTarget(portIndex);
            EnsurePointBuffer();
            float maximumTravelDistance = Mathf.Min(
                Mathf.Max(0f, _port.So.baseRange),
                Mathf.Max(0f, profile.MaximumTravelDistance));
            _trajectorySimulation.PredictTrajectory(
                profile,
                maximumTravelDistance,
                _firePoint.position,
                _firePoint.forward,
                target,
                _points,
                timeStep);

            _lineRenderer.startColor = trajectoryColor;
            _lineRenderer.endColor = trajectoryColor;
            _lineRenderer.startWidth = lineWidth;
            _lineRenderer.endWidth = lineWidth;
            _lineRenderer.positionCount = _points.Length;
            _lineRenderer.SetPositions(_points);
        }

        /// <summary>从父级层级查找依赖并执行一次初始化。</summary>
        private void TryInitializeFromHierarchy()
        {
            if (_initialized)
            {
                return;
            }

            _centralCore = GetComponentInParent<CentralCore>();
            if (_centralCore == null || _centralCore.Ports == null)
            {
                return;
            }

            if (_detector == null)
            {
                _detector = _centralCore.GetComponent<PhysicsCentralDetector>();
            }

            CentralTransmitterRuntime port = FindOwnedPort(_centralCore.Ports);
            if (port != null)
            {
                Initialize(_centralCore, _detector, port);
            }
        }

        /// <summary>按显式端口索引或层级归属查找当前显示对应的端口。</summary>
        /// <param name="ports">中央核心创建的端口集合。</param>
        /// <returns>匹配的端口；没有匹配项时返回 null。</returns>
        private CentralTransmitterRuntime FindOwnedPort(
            System.Collections.Generic.IReadOnlyList<CentralTransmitterRuntime> ports)
        {
            if (portIndex >= 0 && portIndex < ports.Count)
            {
                return ports[portIndex];
            }

            for (int index = 0; index < ports.Count; index++)
            {
                CentralTransmitterRuntime port = ports[index];
                if (port == null || port.FirePoint == null)
                {
                    continue;
                }

                if (transform == port.FirePoint || transform.IsChildOf(port.FirePoint))
                {
                    return port;
                }
            }

            return null;
        }

        /// <summary>确保轨迹采样数组与当前采样步数一致。</summary>
        private void EnsurePointBuffer()
        {
            int pointCount = Mathf.Max(1, predictionSteps);
            if (_points == null || _points.Length != pointCount)
            {
                _points = new Vector3[pointCount];
            }
        }
    }
}
