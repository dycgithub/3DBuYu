using UnityEngine;
using R3;
using Services;
using VContainer;

/// <summary>沿法线约束轨道半径，使用切线方向和弧长驱动球面移动。</summary>
[DefaultExecutionOrder(-100)]
public class SphereWalker : MonoBehaviour
{
    [Header("Sphere 设置")]
    public Vector3 sphereCenter = Vector3.zero;
    public Transform sphereCenterPosObj;
    public float radius = 15f;

    public bool showGizmos;

    [Header("怪物数量与轨道")]
    [Tooltip("横轴为实际存活总数，纵轴为难度初始半径之上的增量。")]
    [SerializeField] private AnimationCurve populationRadiusBonus = AnimationCurve.Linear(0f, 0f, 2003f, 6f);
    [Min(0.1f)] [SerializeField] private float minimumRadius = 1f;
    [Min(0.1f)] [SerializeField] private float maximumRadius = 40f;
    [Min(0.01f)] [SerializeField] private float radiusUnitsPerSecond = 2f;
    private IEnemyPopulationService _population;
    private ICombatPhaseService _phase;
    private System.IDisposable _populationStarted;
    private float _initialRadius = 15f;
    private float _manualOffset;

    /// <summary>绑定数量服务，开局重置难度基准和 Editor 测试偏移。</summary>
    [Inject]
    private void Construct(IEnemyPopulationService population, ICombatPhaseService phase)
    {
        _population = population;
        _phase = phase;
        _populationStarted?.Dispose();
        _populationStarted = population.OnPopulationStarted.Subscribe(_ =>
        {
            _initialRadius = 15f + (int)population.Difficulty;
            _manualOffset = 0f;
            ApplyRadius(_initialRadius);
        });
    }

    /// <summary>即使玩家没有输入也平滑贴合轨道；暂停时保持当前位置。</summary>
    private void Update()
    {
        if (_population == null || _phase == null || !_phase.CanPerformCombatActions)
        {
            return;
        }
        float target = Mathf.Clamp(GetAutomaticRadius() + _manualOffset, minimumRadius, maximumRadius);
        ApplyRadius(Mathf.MoveTowards(radius, target, radiusUnitsPerSecond * Time.deltaTime));
    }

    private float GetAutomaticRadius()
    {
        return _initialRadius + Mathf.Max(0f, populationRadiusBonus.Evaluate(_population?.EnemiesAlive ?? 0));
    }

    /// <summary>组件销毁时解除数量事件订阅。</summary>
    private void OnDestroy() => _populationStarted?.Dispose();

    /// <summary>初始化时将玩家贴合当前轨道。</summary>
    private void Start()
    {
        SnapToSurface();
    }

    public Vector3 GetEffectiveCenter()
    {
        return sphereCenterPosObj != null ? sphereCenterPosObj.position : sphereCenter;
    }

    /// <summary>设置有限正半径，立即保持当前方向贴合球面，静止玩家同样生效。</summary>
    public void SetRadius(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return;
        }
        float clamped = Mathf.Clamp(value, minimumRadius, maximumRadius);
        _manualOffset = clamped - GetAutomaticRadius();
        ApplyRadius(clamped);
    }

    /// <summary>Editor 测试入口，偏移叠加到自动半径，不会下一帧被覆盖。</summary>
    public void AdjustRadius(float delta) => SetRadius(radius + delta);

    /// <summary>清除手动偏移，恢复当前怪物数量决定的轨道。</summary>
    public void ResetRadius()
    {
        _manualOffset = 0f;
        ApplyRadius(GetAutomaticRadius());
    }

    private void ApplyRadius(float value)
    {
        radius = Mathf.Clamp(value, minimumRadius, maximumRadius);
        SnapToSurface();
    }

    /// <summary>沿世界切线移动指定弧长；径向分量被剔除，圆弧可连续穿过两极。</summary>
    /// <param name="direction">世界空间移动方向。</param>
    /// <param name="distance">球面弧长，单位与轨道半径一致。</param>
    public void Move(Vector3 direction, float distance)
    {
        SnapToSurface();
        Vector3 center = GetEffectiveCenter();
        Vector3 normal = (transform.position - center).normalized;
        Vector3 tangent = Vector3.ProjectOnPlane(direction, normal);
        if (tangent.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Vector3 axis = Vector3.Cross(normal, tangent.normalized);
        Quaternion rotation = Quaternion.AngleAxis(distance / radius * Mathf.Rad2Deg, axis);
        transform.position = center + (rotation * normal).normalized * radius;
    }

    public void SnapToSurface()
    {
        Vector3 offsetFromCenter = transform.position - GetEffectiveCenter();
        if (offsetFromCenter.magnitude < 1e-4f)
        {
            offsetFromCenter = Vector3.forward;
        }
        transform.position = GetEffectiveCenter() + offsetFromCenter.normalized * radius;
    }

    private void OnDrawGizmosSelected()
    {
        if (showGizmos)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(sphereCenter, radius);
        }
    }

    private void OnValidate()
    {
        minimumRadius = Mathf.Max(0.1f, minimumRadius);
        maximumRadius = Mathf.Max(minimumRadius, maximumRadius);
        radiusUnitsPerSecond = Mathf.Max(0.01f, radiusUnitsPerSecond);
        radius = Mathf.Clamp(radius, minimumRadius, maximumRadius);
        // 使用单调线性分段，避免曲线切线过冲导致怪物越多轨道反而缩小。
        Keyframe[] keys = populationRadiusBonus.keys;
        for (int index = 0; index < keys.Length; index++)
        {
            keys[index].value = Mathf.Max(index > 0 ? keys[index - 1].value : 0f, keys[index].value);
        }
        for (int index = 0; index < keys.Length; index++)
        {
            keys[index].inTangent = index > 0 ? Slope(keys[index - 1], keys[index]) : 0f;
            keys[index].outTangent = index + 1 < keys.Length ? Slope(keys[index], keys[index + 1]) : 0f;
            keys[index].weightedMode = WeightedMode.None;
        }
        populationRadiusBonus.keys = keys;
    }

    private static float Slope(Keyframe from, Keyframe to)
        => to.time > from.time ? (to.value - from.value) / (to.time - from.time) : 0f;
}
