using GameSystem;
using Play.CombatVisualization.Model;
using Services;
using UnityEngine;
using VContainer;

namespace Play.CombatVisualization.View
{
    /// <summary>
    /// 显示玩家移动球面和自动攻击检测球面。
    /// 视觉范围只读取实际运行时数据，不参与移动或目标查询。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatRangeVisualization : MonoBehaviour
    {
        private const int RingCount = 3;
        private const int MinimumSegmentCount = 8;
        private const float MinimumLineWidth = 0.001f;

        private static readonly Vector3[] RingAxisA =
        {
            Vector3.right,
            Vector3.right,
            Vector3.up
        };

        private static readonly Vector3[] RingAxisB =
        {
            Vector3.up,
            Vector3.forward,
            Vector3.forward
        };

        [Header("依赖")]
        [SerializeField] private SphereWalker _sphereWalker;
        [SerializeField] private PhysicsCentralDetector _detector;

        [Header("线框")]
        [SerializeField, Min(MinimumSegmentCount)] private int _segmentCount = 48;
        [SerializeField, Min(MinimumLineWidth)] private float _lineWidth = 0.06f;
        [SerializeField] private Color _movementColor = new(0.05f, 0.75f, 1f, 0.45f);
        [SerializeField] private Color _detectionColor = new(1f, 0.65f, 0.05f, 0.55f);
        [SerializeField] private Material _rangeMaterial;

        private readonly LineRenderer[] _movementRings = new LineRenderer[RingCount];
        private readonly LineRenderer[] _detectionRings = new LineRenderer[RingCount];
        private Vector3[][] _movementPoints;
        private Vector3[][] _detectionPoints;
        private Vector3 _lastMovementCenter;
        private Vector3 _lastDetectionCenter;
        private float _lastMovementRadius = -1f;
        private float _lastDetectionRadius = -1f;
        private bool _geometryInitialized;
        private bool _isVisible;

        [Inject] private OptionsService options;
        [Inject] private ICombatPhaseService _combatPhase;

        /// <summary>缓存依赖并创建一次性线框对象。</summary>
        private void Awake()
        {
            _sphereWalker ??= GetComponent<SphereWalker>();
            _detector ??= GetComponent<PhysicsCentralDetector>();
            CreateRings(_movementRings, "MovementRange", _movementColor);
            CreateRings(_detectionRings, "DetectionRange", _detectionColor);
            AllocatePointBuffers();
        }

        /// <summary>按当前战斗状态更新范围线位置和可见性。</summary>
        private void LateUpdate()
        {
            if (!ShouldDisplay())
            {
                SetVisible(false);
                return;
            }

            if (_sphereWalker == null || _detector == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            UpdateMovementRings();
            UpdateDetectionRings();
        }

        /// <summary>确保非法编辑器配置不会导致数组和线宽异常。</summary>
        private void OnValidate()
        {
            _segmentCount = Mathf.Max(MinimumSegmentCount, _segmentCount);
            _lineWidth = Mathf.Max(MinimumLineWidth, _lineWidth);
        }

        /// <summary>判断设置和战斗阶段是否允许显示范围。</summary>
        private bool ShouldDisplay()
        {
            if (options != null && !options.Current.showCombatRangeVisuals)
            {
                return false;
            }

            return _combatPhase == null || _combatPhase.CanPerformCombatActions;
        }

        /// <summary>更新移动球面的中心和动态轨道半径。</summary>
        private void UpdateMovementRings()
        {
            Vector3 center = _sphereWalker.GetEffectiveCenter();
            float radius = NormalizeRadius(_sphereWalker.radius);
            if (_geometryInitialized &&
                center == _lastMovementCenter &&
                Mathf.Approximately(radius, _lastMovementRadius))
            {
                return;
            }

            WriteRingGroup(_movementRings, _movementPoints, center, radius);
            _lastMovementCenter = center;
            _lastMovementRadius = radius;
        }

        /// <summary>更新自动攻击检测球面的中心和实际检测半径。</summary>
        private void UpdateDetectionRings()
        {
            Vector3 center = _detector.transform.position;
            float radius = NormalizeRadius(_detector.DetectionRadius);
            if (_geometryInitialized &&
                center == _lastDetectionCenter &&
                Mathf.Approximately(radius, _lastDetectionRadius))
            {
                _geometryInitialized = true;
                return;
            }

            WriteRingGroup(_detectionRings, _detectionPoints, center, radius);
            _lastDetectionCenter = center;
            _lastDetectionRadius = radius;
            _geometryInitialized = true;
        }

        /// <summary>将同一个球面圆环几何写入指定的线框集合。</summary>
        private static void WriteRingGroup(
            LineRenderer[] renderers,
            Vector3[][] points,
            Vector3 center,
            float radius)
        {
            for (int index = 0; index < RingCount; index++)
            {
                SphericalRangeGeometry.FillCircle(
                    center,
                    radius,
                    RingAxisA[index],
                    RingAxisB[index],
                    points[index]);
                renderers[index].SetPositions(points[index]);
            }
        }

        /// <summary>创建指定用途的三条正交环线，并复用共享材质。</summary>
        private void CreateRings(
            LineRenderer[] renderers,
            string groupName,
            Color color)
        {
            for (int index = 0; index < RingCount; index++)
            {
                GameObject ringObject = new GameObject($"{groupName}_{index}");
                ringObject.transform.SetParent(transform, false);
                LineRenderer lineRenderer = ringObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = true;
                lineRenderer.loop = false;
                lineRenderer.startWidth = _lineWidth;
                lineRenderer.endWidth = _lineWidth;
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
                lineRenderer.numCornerVertices = 2;
                lineRenderer.numCapVertices = 2;
                lineRenderer.sharedMaterial = _rangeMaterial;
                lineRenderer.positionCount = 0;
                lineRenderer.enabled = false;
                renderers[index] = lineRenderer;
            }
        }

        /// <summary>按当前采样数量为三组环线分配点位数组。</summary>
        private void AllocatePointBuffers()
        {
            int pointCount = Mathf.Max(MinimumSegmentCount, _segmentCount) + 1;
            _movementPoints = CreatePointBuffers(pointCount);
            _detectionPoints = CreatePointBuffers(pointCount);
        }

        /// <summary>创建固定数量的环线点位缓冲区。</summary>
        private static Vector3[][] CreatePointBuffers(int pointCount)
        {
            Vector3[][] buffers = new Vector3[RingCount][];
            for (int index = 0; index < RingCount; index++)
            {
                buffers[index] = new Vector3[pointCount];
            }

            return buffers;
        }

        /// <summary>切换所有范围线的 Renderer 状态。</summary>
        private void SetVisible(bool visible)
        {
            if (_isVisible == visible)
            {
                return;
            }

            _isVisible = visible;
            SetGroupVisible(_movementRings, visible);
            SetGroupVisible(_detectionRings, visible);
        }

        /// <summary>切换一组环线的 Renderer 状态。</summary>
        private static void SetGroupVisible(LineRenderer[] renderers, bool visible)
        {
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = visible;
                }
            }
        }

        /// <summary>将半径限制为可用于显示的有限非负值。</summary>
        private static float NormalizeRadius(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Max(0f, value);
        }
    }
}
