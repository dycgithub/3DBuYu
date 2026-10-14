using CombatSystem;
using GameSystem;
using Interfaces;
using Services;
using UnityEngine;
using VContainer;
using Play.TargetMark;

namespace Play
{
    /// <summary>
    /// 显示单个端口的轨迹预测，并使用 FirePoint 作为世界射程球心。
    /// <para>端口基础值、装备修正和玩家生成偏移均与实际攻击构建一致。</para>
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public partial class TransmitterTrajectoryDisplay : MonoBehaviour
    {
        [Header("开关")]
        [SerializeField] private bool showTrajectory = true;

        [Header("外观")]
        [SerializeField] private Color trajectoryColor = new Color(0f, 1f, 1f, 0.6f);
        [SerializeField, Range(0.01f, 1f)] private float lineWidth = 0.1f;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private LineRenderer _rangeEndpoint;
        [SerializeField, Min(0.01f)] private float endpointRadius = 0.35f;

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
        private Vector3[] _endpointPoints;
        private float _predictionTimeStep;
        private bool _initialized;

        /// <summary>无状态轨迹算法由当前显示组件直接拥有，不占用容器作用域。</summary>
        private readonly ProjectileTrajectoryPredictor _trajectorySimulation =
            new ProjectileTrajectoryPredictor();
        [Inject] private TransmitterAttackBuildService _attackBuildService;
        [Inject] private ITransmitterGlobalEffectService _globalEffects;
        [Inject] private ISpatialQueryService _spatialQueryService;
        [Inject] private IInputService _input;
        [Inject] private Camera _camera;
        [Inject] private OptionsService options;
        [Inject] private ICombatPhaseService _combatPhase;
        [Inject] private MarkedTargetService _markedTarget;

        /// <summary>获取当前轨迹显示绑定的端口索引。</summary>
        public int PortIndex => portIndex;

        /// <summary>缓存轨迹线并准备射程终点标记。</summary>
        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 0;
            if (lineMaterial != null)
            {
                _lineRenderer.sharedMaterial = lineMaterial;
            }

            _rangeEndpoint ??= transform.Find("RangeEndpoint")?.GetComponent<LineRenderer>();
            if (_rangeEndpoint == null)
            {
                GameObject endpointObject = new GameObject("RangeEndpoint");
                endpointObject.transform.SetParent(transform, false);
                _rangeEndpoint = endpointObject.AddComponent<LineRenderer>();
            }

            ConfigureEndpoint();
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

        /// <summary>按当前端口、开关和战斗状态更新轨迹及射程终点。</summary>
        private void LateUpdate()
        {
            if (!showTrajectory || !ShouldDisplay())
            {
                HideVisuals();
                return;
            }

            if (!_initialized)
            {
                TryInitializeFromHierarchy();
            }

            if (!_initialized || _port.IsLocked || _firePoint == null ||
                _trajectorySimulation == null)
            {
                HideVisuals();
                return;
            }

            IDamageable target = ResolveAimTarget();
            Vector3 rangeOrigin = _firePoint.position;
            Vector3 launchDirection = _firePoint.forward;

            ProjectileProfileSO profile = _port.So.defaultProjectileProfile;
            float rangeMultiplier = 1f;
            float collisionRadius = profile != null
                ? profile.CollisionRadius
                : 0f;
            int projectileCount = Mathf.Max(1, _port.So.projectileCount);
            if (_attackBuildService != null)
            {
                TransmitterAttackInput attackInput = new(
                    _centralCore.GetInstanceID(),
                    _port.PortIndex,
                    profile,
                    rangeOrigin,
                    launchDirection,
                    _port.So.baseDamage,
                    _port.So.baseRange,
                    _port.So.attacksPerSecond,
                    _port.So.projectileCount,
                    _port.So.penetration,
                    _port.So.criticalChance,
                    _port.So.criticalDamage,
                    _port.PortId,
                    _port.FireSound,
                    _port.FireEffectId,
                    _firePoint,
                    target,
                    _port.So.projectileSpawnOffset);
                TransmitterAttackBuild attackBuild = _attackBuildService.BuildAttackParameters(
                    in attackInput);
                if (attackBuild.ProjectileProfile != null)
                {
                    profile = attackBuild.ProjectileProfile;
                }

                rangeMultiplier = attackBuild.RangeMultiplier;
                collisionRadius = attackBuild.CollisionRadius;
                projectileCount = Mathf.Max(1, attackBuild.ProjectileCount);
            }

            if (profile == null)
            {
                HideVisuals();
                return;
            }

            float maximumTravelDistance = Mathf.Min(
                NormalizeDistance(_port.So.baseRange) * Mathf.Max(0f, rangeMultiplier),
                NormalizeDistance(profile.MaximumTravelDistance));
            maximumTravelDistance = NormalizeDistance(maximumTravelDistance);
            EnsurePointBuffer(profile, maximumTravelDistance);

            if (profile.EmitProjectilesSequentially && profile.SequentialProjectileCount > 0)
            {
                projectileCount = profile.SequentialProjectileCount;
            }

            Vector3 projectileOrigin = ProjectileLaunchPattern.GetOriginPosition(
                rangeOrigin,
                launchDirection,
                projectileCount,
                0,
                ProjectileLaunchPattern.GetMinimumSpacing(profile, collisionRadius));
            float scatterAngle = profile.EmitProjectilesSequentially
                ? 0f
                : profile.ScatterAngleDegrees;
            Vector3 projectileDirection = ProjectileLaunchPattern.GetDirection(
                launchDirection,
                projectileCount,
                0,
                scatterAngle);
            if (target.IsAliveAndValid())
            {
                Vector3 targetDirection = target.Position - projectileOrigin;
                projectileDirection = targetDirection.sqrMagnitude > 0.0001f
                    ? targetDirection.normalized
                    : launchDirection;
            }

            if (projectileDirection.sqrMagnitude <= 0.0001f)
            {
                projectileDirection = launchDirection;
            }

            projectileDirection.Normalize();
            Vector3 startPosition = projectileOrigin + projectileDirection *
                Mathf.Max(0f, _port.So.projectileSpawnOffset);
            _trajectorySimulation.PredictTrajectoryWithinRange(
                profile,
                maximumTravelDistance,
                rangeOrigin,
                startPosition,
                projectileDirection,
                target,
                _points,
                _predictionTimeStep,
                _spatialQueryService);

            _lineRenderer.startColor = trajectoryColor;
            _lineRenderer.endColor = trajectoryColor;
            _lineRenderer.startWidth = Mathf.Max(0.01f, lineWidth);
            _lineRenderer.endWidth = Mathf.Max(0.01f, lineWidth);
            _lineRenderer.positionCount = _points.Length;
            _lineRenderer.SetPositions(_points);
            UpdateRangeEndpoint(
                projectileDirection,
                profile,
                rangeOrigin,
                maximumTravelDistance);
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

        /// <summary>按轨迹总路径设置采样时间，回旋镖包含完整往返。</summary>
        private void EnsurePointBuffer(
            ProjectileProfileSO profile,
            float maximumTravelDistance)
        {
            int pointCount = Mathf.Max(2, predictionSteps);
            if (_points == null || _points.Length != pointCount)
            {
                _points = new Vector3[pointCount];
            }

            _predictionTimeStep = ProjectileTrajectoryPredictor.ResolveTimeStep(
                profile,
                maximumTravelDistance,
                timeStep,
                pointCount);
        }

        /// <summary>配置射程终点环线的渲染参数。</summary>
        private void ConfigureEndpoint()
        {
            if (_rangeEndpoint == null)
            {
                return;
            }

            _rangeEndpoint.useWorldSpace = true;
            _rangeEndpoint.loop = false;
            _rangeEndpoint.positionCount = 0;
            _rangeEndpoint.startColor = trajectoryColor;
            _rangeEndpoint.endColor = trajectoryColor;
            _rangeEndpoint.startWidth = Mathf.Max(0.01f, lineWidth * 0.75f);
            _rangeEndpoint.endWidth = Mathf.Max(0.01f, lineWidth * 0.75f);
            if (lineMaterial != null)
            {
                _rangeEndpoint.sharedMaterial = lineMaterial;
            }
        }

        /// <summary>将射程边界写成垂直于弹道方向的发光圆环。</summary>
        /// <param name="direction">本次发射的中心方向。</param>
        /// <param name="profile">最终装备合成的投射物配置。</param>
        /// <param name="rangeOrigin">FirePoint 世界射程球心。</param>
        /// <param name="maximumTravelDistance">本次攻击的有效射程。</param>
        private void UpdateRangeEndpoint(
            Vector3 direction,
            ProjectileProfileSO profile,
            Vector3 rangeOrigin,
            float maximumTravelDistance)
        {
            if (_rangeEndpoint == null || _points == null || _points.Length == 0)
            {
                return;
            }

            const int endpointSegments = 20;
            int pointCount = endpointSegments + 1;
            if (_endpointPoints == null || _endpointPoints.Length != pointCount)
            {
                _endpointPoints = new Vector3[pointCount];
            }

            Vector3 forward = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            Vector3 right = Vector3.Cross(forward, Vector3.up);
            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.Cross(forward, Vector3.right);
            }

            right.Normalize();
            Vector3 up = Vector3.Cross(forward, right).normalized;
            float radius = Mathf.Max(0.01f, endpointRadius);
            Vector3 center = profile.TrajectoryDefinition is BoomerangTrajectoryDefinitionSO
                ? rangeOrigin + forward * maximumTravelDistance
                : _points[_points.Length - 1];
            for (int index = 0; index < pointCount; index++)
            {
                float angle = index / (float)endpointSegments * Mathf.PI * 2f;
                _endpointPoints[index] = center +
                    (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
            }

            _rangeEndpoint.positionCount = _endpointPoints.Length;
            _rangeEndpoint.SetPositions(_endpointPoints);
        }

        /// <summary>隐藏轨迹和射程终点。</summary>
        private void HideVisuals()
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
            }

            if (_rangeEndpoint != null)
            {
                _rangeEndpoint.positionCount = 0;
            }
        }

        /// <summary>判断全局设置和战斗阶段是否允许显示。</summary>
        private bool ShouldDisplay()
        {
            if (options != null && !options.Current.showCombatRangeVisuals)
            {
                return false;
            }

            return _combatPhase == null || _combatPhase.CanPerformCombatActions;
        }

        /// <summary>将距离配置限制为有限非负值。</summary>
        private static float NormalizeDistance(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Max(0f, value);
        }

        /// <summary>将采样时间步长限制为有效值。</summary>
        private static float NormalizeTimeStep(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0.05f;
            }

            return Mathf.Max(0.01f, value);
        }

        private void OnValidate()
        {
            endpointRadius = Mathf.Max(0.01f, endpointRadius);
            lineWidth = Mathf.Max(0.01f, lineWidth);
            predictionSteps = Mathf.Max(2, predictionSteps);
            timeStep = NormalizeTimeStep(timeStep);
        }
    }
}
