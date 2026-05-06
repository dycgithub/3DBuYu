# 相机系统 (Camera System) 规格说明书

## 模块概述

相机系统负责管理游戏中的摄像机行为，包括跟随玩家、视角控制、震动效果等。系统提供多种相机模式以适应不同的游戏场景。

## 子系统列表

1. **CameraFollow** - 通用相机跟随
2. **SphereCameraController** - 球面移动相机控制器
3. **CameraShake** - 相机震动效果

## CameraFollow 规格

### 相机模式

| 模式 | 说明 | 适用场景 |
|------|------|----------|
| `FixedDistance` | 固定距离跟随 | 平面移动 |
| `Orbit` | 轨道旋转 | 自由视角 |
| `Smooth` | 平滑跟随 | 快速移动 |
| `LookAt` | 仅注视 | 固定位置观察 |

### 核心属性

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `target` | Transform | null | 跟随目标 |
| `mode` | CameraMode | Smooth | 相机模式 |
| `distance` | float | 10f | 距离 |
| `height` | float | 5f | 高度偏移 |
| `smoothTime` | float | 0.3f | 平滑时间 |
| `maxSpeed` | float | 100f | 最大速度 |
| `rotationSpeed` | float | 5f | 旋转速度 |

### 平滑跟随实现

```csharp
public class CameraFollow : MonoBehaviour
{
    public enum CameraMode
    {
        FixedDistance,
        Orbit,
        Smooth,
        LookAt
    }

    [Header("目标")]
    public Transform target;

    [Header("模式")]
    public CameraMode mode = CameraMode.Smooth;

    [Header("距离和高度")]
    public float distance = 10f;
    public float height = 5f;

    [Header("平滑设置")]
    public float smoothTime = 0.3f;
    public float maxSpeed = 100f;

    [Header("轨道设置")]
    public float rotationSpeed = 5f;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 60f;

    // 内部状态
    private Vector3 currentVelocity;
    private float currentRotationX;
    private float currentRotationY;

    void LateUpdate()
    {
        if (target == null) return;

        switch (mode)
        {
            case CameraMode.FixedDistance:
                FixedDistanceFollow();
                break;
            case CameraMode.Orbit:
                OrbitFollow();
                break;
            case CameraMode.Smooth:
                SmoothFollow();
                break;
            case CameraMode.LookAt:
                LookAtTarget();
                break;
        }
    }

    void SmoothFollow()
    {
        // 计算目标位置（目标后方）
        Vector3 targetPosition = target.position - target.forward * distance + Vector3.up * height;

        // 平滑移动
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            smoothTime,
            maxSpeed,
            Time.deltaTime
        );

        // 注视目标
        transform.LookAt(target.position + Vector3.up * height * 0.5f);
    }

    void OrbitFollow()
    {
        // 处理输入
        float horizontal = Input.GetAxis("Mouse X") * rotationSpeed;
        float vertical = Input.GetAxis("Mouse Y") * rotationSpeed;

        currentRotationX += horizontal;
        currentRotationY -= vertical;
        currentRotationY = Mathf.Clamp(currentRotationY, minVerticalAngle, maxVerticalAngle);

        // 计算相机位置
        Quaternion rotation = Quaternion.Euler(currentRotationY, currentRotationX, 0);
        Vector3 offset = rotation * Vector3.back * distance;
        Vector3 position = target.position + offset;

        transform.position = position;
        transform.LookAt(target.position);
    }
}
```

## SphereCameraController 规格

### 球面相机特性

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `sphereCenter` | Vector3 | (0,0,0) | 球心位置 |
| `sphereRadius` | float | 50f | 球体半径 |
| `followHeight` | float | 15f | 相机高度偏移 |
| `followDistance` | float | 20f | 相机距离 |
| `rotationSmoothing` | float | 0.1f | 旋转平滑度 |

### 球面跟随实现

```csharp
public class SphereCameraController : MonoBehaviour
{
    [Header("跟随目标")]
    public Transform target;

    [Header("球面设置")]
    public Vector3 sphereCenter = Vector3.zero;
    public float sphereRadius = 50f;

    [Header("相机偏移")]
    public float followHeight = 15f;
    public float followDistance = 20f;

    [Header("平滑设置")]
    public float positionSmoothing = 0.1f;
    public float rotationSmoothing = 0.1f;

    private Vector3 targetPosition;
    private Vector3 currentVelocity;

    void LateUpdate()
    {
        if (target == null) return;

        // 计算相机在球面上的位置
        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        // 获取从球心到玩家的方向
        Vector3 toPlayer = (target.position - sphereCenter).normalized;

        // 计算相机目标位置（玩家后方、上方）
        Vector3 backward = -target.forward;
        Vector3 up = toPlayer;
        Vector3 right = Vector3.Cross(up, backward).normalized;
        backward = Vector3.Cross(right, up).normalized;

        // 组合方向
        Vector3 cameraDirection = (backward * followDistance + up * followHeight).normalized;

        // 计算目标位置（在球面上）
        Vector3 targetPos = sphereCenter + cameraDirection * (sphereRadius + followHeight);

        // 平滑移动
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref currentVelocity,
            positionSmoothing
        );

        // 平滑旋转看向目标
        Quaternion targetRotation = Quaternion.LookRotation(
            target.position - transform.position,
            (transform.position - sphereCenter).normalized
        );
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSmoothing * Time.deltaTime
        );
    }
}
```

## 依赖关系

```
CameraSystem
├── UnityEngine (核心)
└── Movement/SphericalCoordinates (球面相机)
```

## 测试要点

1. **功能测试**: 各模式跟随行为、平滑度、边界处理
2. **性能测试**: 大量移动时的相机性能
3. **边界测试**: 目标丢失/销毁、相机碰撞处理
