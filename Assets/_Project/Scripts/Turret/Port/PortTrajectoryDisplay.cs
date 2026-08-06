using UnityEngine;
using Interfaces;
using VContainer;
using Services;

namespace TurretSystem
{
    [RequireComponent(typeof(LineRenderer))]
    public class PortTrajectoryDisplay : MonoBehaviour
    {
        [Header("开关")]
        [SerializeField] private bool showTrajectory = true;

        [Header("外观")]
        [SerializeField] private Color trajectoryColor = new Color(0f, 1f, 1f, 0.6f);
        [SerializeField][Range(0.01f, 1f)] private float lineWidth = 0.1f;
        [SerializeField] private Material lineMaterial;

        [Header("采样")]
        [SerializeField][Range(10, 200)] private int predictionSteps = 60;
        [SerializeField][Range(0.01f, 0.2f)] private float timeStep = 0.05f;

        [Header("配置源")]
        [SerializeField] private PortFireController fireController;

        private LineRenderer _lineRenderer;
        private PhysicsTurretDetector _detector;
        private Transform _firePoint;

        [Inject] private ITrajectorySimulationService _simulation;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 0;
            if (lineMaterial != null) _lineRenderer.material = lineMaterial;
        }

        private void Start()
        {
            if (!showTrajectory) { enabled = false; return; }
            var turret = GetComponentInParent<Turret>();
            if (turret != null)
            {
                _detector = turret.GetComponent<PhysicsTurretDetector>();
                if (fireController == null)
                    fireController = turret.GetComponent<PortFireController>();
            }
            _firePoint = transform.parent;
        }

        private void LateUpdate()
        {
            if (_firePoint == null || _simulation == null || fireController?.bulletProfile == null)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            var profile = fireController.bulletProfile;
            var target = FindTargetForPort();
            var points = _simulation.Simulate(
                profile, _firePoint.position, _firePoint.forward,
                target, predictionSteps, timeStep);

            _lineRenderer.startColor = trajectoryColor;
            _lineRenderer.endColor = trajectoryColor;
            _lineRenderer.startWidth = lineWidth;
            _lineRenderer.endWidth = lineWidth;

            if (points != null && points.Length > 0)
            {
                _lineRenderer.positionCount = points.Length;
                _lineRenderer.SetPositions(points);
            }
            else
            {
                _lineRenderer.positionCount = 0;
            }
        }

        private IDamageable FindTargetForPort()
        {
            if (_detector == null || _firePoint == null) return null;
            var turret = GetComponentInParent<Turret>();
            if (turret == null) return null;
            for (int i = 0; i < turret.PortCount; i++)
            {
                var port = turret.GetPort(i);
                if (port != null && port.FirePoint == _firePoint)
                    return _detector.GetTarget(i);
            }
            return null;
        }
    }
}
