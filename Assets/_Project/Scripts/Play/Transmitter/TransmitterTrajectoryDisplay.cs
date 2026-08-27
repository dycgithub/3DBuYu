using Interfaces;
using Services;
using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>
    /// 显示单个端口的轨迹预测。
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

        [Header("配置源")]
        [SerializeField] private TransmitterFireController fireController;
        [SerializeField, Min(-1)] private int portIndex = -1;

        private LineRenderer _lineRenderer;
        private CentralCore _centralCore;
        private PhysicsCentralDetector _detector;
        private CentralTransmitterRuntime _port;
        private Transform _firePoint;
        private Vector3[] _points;
        private bool _initialized;

        [Inject] private ITrajectorySimulationService _simulation;

        public int PortIndex => portIndex;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 0;
            if (lineMaterial != null)
                _lineRenderer.material = lineMaterial;
        }

        /// <summary>
        /// 由 CentralCore 绑定所属端口，避免显示组件在每帧反查父级和端口集合。
        /// </summary>
        public void Initialize(
            CentralCore core,
            PhysicsCentralDetector detector,
            TransmitterFireController controller,
            CentralTransmitterRuntime port)
        {
            _centralCore = core;
            _detector = detector;
            fireController = controller != null ? controller : fireController;
            _port = port;

            if (_port != null)
            {
                portIndex = _port.PortIndex;
                _firePoint = _port.FirePoint;
            }

            _initialized = _centralCore != null && _detector != null && _port != null;
        }

        private void Start()
        {
            if (!showTrajectory)
            {
                enabled = false;
                return;
            }

            // 兼容未由 CentralCore 主动绑定的旧预制体，只在启动时解析一次。
            TryInitializeFromHierarchy();
        }

        private void LateUpdate()
        {
            if (!_initialized || _firePoint == null || _simulation == null || fireController == null ||
                fireController.BulletProfile == null)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            IDamageable target = _detector.GetTarget(portIndex);
            EnsurePointBuffer();
            _simulation.Simulate(
                fireController.BulletProfile,
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

        private void TryInitializeFromHierarchy()
        {
            if (_initialized)
                return;

            _centralCore = GetComponentInParent<CentralCore>();
            if (_centralCore == null || _centralCore.Ports == null)
                return;

            if (_detector == null)
                _detector = _centralCore.GetComponent<PhysicsCentralDetector>();
            if (fireController == null)
                fireController = _centralCore.GetComponent<TransmitterFireController>();

            CentralTransmitterRuntime port = FindOwnedPort(_centralCore.Ports);
            if (port != null)
                Initialize(_centralCore, _detector, fireController, port);
        }

        private CentralTransmitterRuntime FindOwnedPort(
            System.Collections.Generic.IReadOnlyList<CentralTransmitterRuntime> ports)
        {
            if (portIndex >= 0 && portIndex < ports.Count)
                return ports[portIndex];

            for (int i = 0; i < ports.Count; i++)
            {
                CentralTransmitterRuntime port = ports[i];
                if (port == null || port.FirePoint == null)
                    continue;

                if (transform == port.FirePoint || transform.IsChildOf(port.FirePoint))
                    return port;
            }

            return null;
        }

        private void EnsurePointBuffer()
        {
            int pointCount = Mathf.Max(1, predictionSteps);
            if (_points == null || _points.Length != pointCount)
                _points = new Vector3[pointCount];
        }
    }
}
