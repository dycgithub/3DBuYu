using UnityEngine;

/// <summary>
/// 摄影机跟随控制器
/// 跟随移动的物体，支持平滑过渡和多种跟随模式
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("目标设置")]
    [Tooltip("要跟随的目标物体")]
    public Transform target;

    [Tooltip("目标上的跟随点（可选，如果不设置则使用target位置）")]
    public Transform targetFollowPoint;

    [Header("跟随模式")]
    [Tooltip("跟随模式：FixedDistance-固定距离，Orbit-轨道跟随，Smooth-平滑跟随")]
    public FollowMode followMode = FollowMode.Smooth;

    [Tooltip("跟随模式枚举")]
    public enum FollowMode { FixedDistance, Orbit, Smooth }

    [Header("位置设置")]
    [Tooltip("摄影机与目标的距离")]
    public float distance = 10f;

    [Tooltip("最小距离")]
    public float minDistance = 3f;

    [Tooltip("最大距离")]
    public float maxDistance = 20f;

    [Tooltip("高度偏移")]
    public float heightOffset = 2f;

    [Tooltip("水平角度偏移（度）")]
    public float horizontalAngleOffset;

    [Tooltip("垂直角度偏移（度）")]
    public float verticalAngleOffset = -20f;

    [Header("平滑设置")]
    [Tooltip("位置平滑时间")]
    public float positionSmoothTime = 0.1f;

    [Tooltip("旋转平滑时间")]
    public float rotationSmoothTime = 0.1f;

    [Tooltip("使用阻尼跟随")]
    public bool useDamping = true;

    [Header("轨道设置")]
    [Tooltip("轨道旋转速度（度/秒）")]
    public float orbitSpeed = 50f;

    [Tooltip("自动轨道旋转")]
    public bool autoOrbit;

    [Tooltip("自动轨道速度")]
    public float autoOrbitSpeed = 5f;

    [Header("限制设置")]
    [Tooltip("最小垂直角度")]
    public float minVerticalAngle = -85f;

    [Tooltip("最大垂直角度")]
    public float maxVerticalAngle = 85f;

    [Header("球体设置")]
    [Tooltip("球心位置（用于球面跟随）")]
    public Vector3 sphereCenter = Vector3.zero;

    [Tooltip("保持与球心的最小距离")]
    public float minDistanceToCenter = 5f;

    // 内部变量
    private Vector3 _currentVelocity;
    private float _currentDistance;
    private float _currentHorizontalAngle;
    private float _currentVerticalAngle;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private bool _hasTarget;

    // 鼠标控制
    private float _mouseX;
    private float _mouseY;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraFollow: 没有设置目标物体！");
            return;
        }

        _hasTarget = true;
        _currentDistance = distance;

        // 初始化角度
        Vector3 direction = transform.position - target.position;
        _currentHorizontalAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        _currentVerticalAngle = Mathf.Asin(direction.y / direction.magnitude) * Mathf.Rad2Deg;

        // 应用初始偏移
        _currentHorizontalAngle += horizontalAngleOffset;
        _currentVerticalAngle += verticalAngleOffset;
        ClampVerticalAngle();
    }

    private void LateUpdate()
    {
        if (!_hasTarget || target == null) return;

        HandleInput();
        UpdateFollow();
    }

    private void HandleInput()
    {
        // 鼠标右键拖动旋转
        if (Input.GetMouseButton(1))
        {
            _mouseX += Input.GetAxis("Mouse X") * orbitSpeed;
            _mouseY -= Input.GetAxis("Mouse Y") * orbitSpeed;
            _mouseY = Mathf.Clamp(_mouseY, minVerticalAngle, maxVerticalAngle);
        }

        // 滚轮调整距离
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            _currentDistance -= scroll * distance * 0.5f;
            _currentDistance = Mathf.Clamp(_currentDistance, minDistance, maxDistance);
        }

        // 自动轨道
        if (autoOrbit)
        {
            _mouseX += autoOrbitSpeed * Time.deltaTime;
        }
    }

    private void UpdateFollow()
    {
        Vector3 targetPos = GetTargetPosition();

        switch (followMode)
        {
            case FollowMode.FixedDistance:
                UpdateFixedDistance(targetPos);
                break;
            case FollowMode.Orbit:
                UpdateOrbit(targetPos);
                break;
            case FollowMode.Smooth:
                UpdateSmooth(targetPos);
                break;
        }

        // 应用位置和旋转
        transform.position = _targetPosition;
        transform.rotation = _targetRotation;
    }

    private Vector3 GetTargetPosition()
    {
        if (targetFollowPoint != null)
        {
            return targetFollowPoint.position;
        }
        return target.position;
    }

    private void UpdateFixedDistance(Vector3 targetPos)
    {
        // 计算摄影机位置（在球面上）
        Vector3 direction = GetDirectionFromAngles(_currentHorizontalAngle, _currentVerticalAngle);
        _targetPosition = targetPos - direction * _currentDistance;
        _targetPosition += Vector3.up * heightOffset;

        // 确保与球心保持距离
        if (sphereCenter != Vector3.zero)
        {
            Vector3 toCamera = _targetPosition - sphereCenter;
            float distToCenter = toCamera.magnitude;
            if (distToCenter < minDistanceToCenter)
            {
                _targetPosition = sphereCenter + toCamera.normalized * minDistanceToCenter;
            }
        }

        _targetRotation = Quaternion.LookRotation(targetPos - _targetPosition);
    }

    private void UpdateOrbit(Vector3 targetPos)
    {
        // 使用当前鼠标/自动控制的旋转
        _currentHorizontalAngle = _mouseX;
        _currentVerticalAngle = _mouseY;

        UpdateFixedDistance(targetPos);
    }

    private void UpdateSmooth(Vector3 targetPos)
    {
        // 计算理想位置
        Vector3 idealDirection = GetDirectionFromAngles(
            _currentHorizontalAngle + horizontalAngleOffset,
            _currentVerticalAngle + verticalAngleOffset);

        Vector3 idealPosition = targetPos - idealDirection * _currentDistance + Vector3.up * heightOffset;

        // 平滑移动到理想位置
        if (useDamping)
        {
            _targetPosition = Vector3.SmoothDamp(transform.position, idealPosition,
                ref _currentVelocity, positionSmoothTime);
        }
        else
        {
            _targetPosition = idealPosition;
        }

        // 平滑旋转
        Quaternion idealRotation = Quaternion.LookRotation(targetPos - _targetPosition);
        _targetRotation = Quaternion.Slerp(transform.rotation, idealRotation,
            rotationSmoothTime > 0 ? Time.deltaTime / rotationSmoothTime : 1f);
    }

    private Vector3 GetDirectionFromAngles(float horizontal, float vertical)
    {
        float horizontalRad = horizontal * Mathf.Deg2Rad;
        float verticalRad = Mathf.Clamp(vertical, -89f, 89f) * Mathf.Deg2Rad;

        float x = Mathf.Cos(verticalRad) * Mathf.Sin(horizontalRad);
        float y = Mathf.Sin(verticalRad);
        float z = Mathf.Cos(verticalRad) * Mathf.Cos(horizontalRad);

        return new Vector3(x, y, z).normalized;
    }

    private void ClampVerticalAngle()
    {
        _currentVerticalAngle = Mathf.Clamp(_currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
    }

    /// <summary>
    /// 设置目标物体
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _hasTarget = newTarget != null;
    }

    /// <summary>
    /// 设置距离
    /// </summary>
    public void SetDistance(float newDistance)
    {
        distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
        _currentDistance = distance;
    }

    /// <summary>
    /// 重置到默认视角
    /// </summary>
    public void ResetView()
    {
        _currentHorizontalAngle = 0f;
        _currentVerticalAngle = -30f;
        _currentDistance = distance;
    }

    /// <summary>
    /// 聚焦于目标
    /// </summary>
    public void FocusOnTarget()
    {
        if (target == null) return;
        ResetView();
    }
}
