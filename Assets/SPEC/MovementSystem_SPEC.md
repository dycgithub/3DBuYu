# 移动系统 (Movement System) 规格说明书

## 模块概述

移动系统负责处理玩家在球形世界表面的移动、旋转和相机跟随。系统基于球坐标系实现，使玩家能够在3D球体表面自由移动。

## 架构设计

### 核心组件

```
┌─────────────────────────────────────────────────────┐
│              SphereMovement                        │
│  (球面移动控制器 - 基于球坐标系)                    │
├─────────────────────────────────────────────────────┤
│  - 球坐标转换 (经度/纬度/半径)                      │
│  - 重力方向计算 (始终指向球心)                      │
│  - 极点附近平滑处理 (避免万向锁)                    │
│  - 移动输入响应 (WASD/摇杆)                         │
└─────────────────────────────────────────────────────┘
```

### 类图

```
┌──────────────────┐      ┌──────────────────────┐
│  SphereMovement  │◀────▶│  SphericalCoordinates │
│   (Controller)    │      │    (Math Utility)    │
└──────────────────┘      └──────────────────────┘
         │
         ▼
┌──────────────────┐      ┌──────────────────────┐
│ SphereCamera     │◀────▶│   CameraFollow       │
│ (Camera Control) │      │  (General Purpose)   │
└──────────────────┘      └──────────────────────┘
```

## 核心功能规格

### 1. 球坐标系统 (SphericalCoordinates)

| 属性 | 类型 | 说明 |
|------|------|------|
| `longitude` (λ) | float | 经度 (0° ~ 360°) |
| `latitude` (φ) | float | 纬度 (-90° ~ 90°) |
| `radius` (r) | float | 到球心的距离 |

**坐标转换**:

```csharp
// 球坐标 → 笛卡尔坐标
public static Vector3 SphericalToCartesian(float longitude, float latitude, float radius)
{
    float lonRad = longitude * Mathf.Deg2Rad;
    float latRad = latitude * Mathf.Deg2Rad;

    float x = radius * Mathf.Cos(latRad) * Mathf.Cos(lonRad);
    float y = radius * Mathf.Sin(latRad);
    float z = radius * Mathf.Cos(latRad) * Mathf.Sin(lonRad);

    return new Vector3(x, y, z);
}

// 笛卡尔坐标 → 球坐标
public static void CartesianToSpherical(Vector3 cartesian, out float longitude, out float latitude, out float radius)
{
    radius = cartesian.magnitude;
    longitude = Mathf.Atan2(cartesian.z, cartesian.x) * Mathf.Rad2Deg;
    latitude = Mathf.Asin(cartesian.y / radius) * Mathf.Rad2Deg;
}
```

### 2. 球面移动 (SphereMovement)

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `sphereCenter` | Vector3 | (0,0,0) | 球心位置 |
| `sphereRadius` | float | 10f | 球体半径 |
| `moveSpeed` | float | 5f | 移动速度 |
| `rotationSpeed` | float | 10f | 旋转速度 |
| `poleSmoothing` | bool | true | 极点附近平滑处理 |

**核心移动逻辑**:

```csharp
private void MoveOnSphere()
{
    // 获取输入
    float horizontal = Input.GetAxis("Horizontal");
    float vertical = Input.GetAxis("Vertical");

    if (horizontal == 0 && vertical == 0) return;

    // 计算移动方向（基于当前朝向）
    Vector3 forward = GetForwardDirection();
    Vector3 right = Vector3.Cross(GetUpDirection(), forward);

    Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;

    // 计算球面上的新位置
    Vector3 newPosition = transform.position + moveDirection * moveSpeed * Time.deltaTime;

    // 投影到球面
    Vector3 toCenter = sphereCenter - newPosition;
    float currentRadius = toCenter.magnitude;
    newPosition = sphereCenter - toCenter.normalized * sphereRadius;

    // 极点附近平滑处理
    if (poleSmoothing)
    {
        float latitude = Mathf.Asin(Vector3.Dot(toCenter.normalized, Vector3.up));
        float poleThreshold = 85f * Mathf.Deg2Rad;

        if (Mathf.Abs(latitude) > poleThreshold)
        {
            // 在极点附近降低速度并平滑转向
            float t = (Mathf.Abs(latitude) - poleThreshold) / (5f * Mathf.Deg2Rad);
            moveSpeed *= Mathf.Lerp(1f, 0.2f, t);
        }
    }

    // 应用新位置
    transform.position = newPosition;

    // 更新朝向
    Quaternion targetRotation = Quaternion.LookRotation(moveDirection, GetUpDirection());
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
}
```

### 3. 相机跟随 (SphereCameraController)

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `target` | Transform | null | 跟随目标 |
| `distance` | float | 15f | 摄像机距离 |
| `height` | float | 5f | 高度偏移 |
| `followSpeed` | float | 5f | 跟随速度 |
| `lookAtOffset` | Vector3 | (0,2,0) | 看向偏移 |

**相机跟随逻辑**:

```csharp
private void FollowTarget()
{
    if (target == null) return;

    // 计算目标后方位置
    Vector3 targetBack = -target.forward;
    Vector3 desiredPosition = target.position + targetBack * distance + Vector3.up * height;

    // 平滑移动
    transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

    // 看向目标
    Vector3 lookAtPosition = target.position + lookAtOffset;
    transform.LookAt(lookAtPosition);
}
```

## 接口定义

### 输入接口

```csharp
/// <summary>
/// 移动系统输入接口
/// </summary>
public interface IMovementInput
{
    /// <summary>
    /// 水平输入 (-1 ~ 1)
    /// </summary>    float Horizontal { get; }

    /// <summary>
    /// 垂直输入 (-1 ~ 1)
    /// </summary>
    float Vertical { get; }

    /// <summary>
    /// 是否有输入
    /// </summary>
    bool HasInput { get; }
}

/// <summary>
/// 键盘输入实现
/// </summary>
public class KeyboardInput : IMovementInput
{
    public float Horizontal => Input.GetAxis("Horizontal");
    public float Vertical => Input.GetAxis("Vertical");
    public bool HasInput => Horizontal != 0 || Vertical != 0;
}
```

## 性能优化

### 1. 缓存计算

```csharp
// 缓存常用值
private Vector3 cachedUpDirection;
private float lastCacheTime;
private const float CACHE_INTERVAL = 0.1f;

private void UpdateCache()
{
    if (Time.time - lastCacheTime > CACHE_INTERVAL)
    {
        cachedUpDirection = (transform.position - sphereCenter).normalized;
        lastCacheTime = Time.time;
    }
}
```

### 2. 距离平方比较

```csharp
// 使用平方距离避免开方运算
public bool IsInRange(Vector3 target, float range)
{
    float sqrRange = range * range;
    float sqrDistance = (target - transform.position).sqrMagnitude;
    return sqrDistance <= sqrRange;
}
```

### 3. 固定更新频率

```csharp
// 降低移动计算频率
private float lastMoveUpdate;
private const float MOVE_UPDATE_INTERVAL = 0.016f; // ~60fps

void Update()
{
    if (Time.time - lastMoveUpdate >= MOVE_UPDATE_INTERVAL)
    {
        MoveOnSphere();
        lastMoveUpdate = Time.time;
    }
}
```

## 调试工具

### 可视化调试 (Gizmos)

```csharp
void OnDrawGizmosSelected()
{
    // 绘制球体
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireSphere(sphereCenter, sphereRadius);

    // 绘制当前位置到球心的连线
    Gizmos.color = Color.yellow;
    Gizmos.DrawLine(transform.position, sphereCenter);

    // 绘制移动方向
    Gizmos.color = Color.green;
    Vector3 forward = GetForwardDirection() * 3f;
    Gizmos.DrawRay(transform.position, forward);

    // 绘制上方向
    Gizmos.color = Color.red;
    Vector3 up = GetUpDirection() * 2f;
    Gizmos.DrawRay(transform.position, up);
}
```

## 配置示例

```json
{
  "movement": {
    "sphereRadius": 50,
    "moveSpeed": 8,
    "rotationSpeed": 10,
    "poleSmoothing": true,
    "poleThreshold": 85
  },
  "camera": {
    "distance": 20,
    "height": 8,
    "followSpeed": 5,
    "lookAtOffset": {"x": 0, "y": 2, "z": 0}
  },
  "input": {
    "keyboardEnabled": true,
    "gamepadEnabled": true,
    "invertY": false,
    "sensitivity": 1.0
  }
}
```

## 依赖关系

```
MovementSystem
├── Utils/SphericalCoordinates (数学工具)
└── Camera/CameraFollow (相机跟随)
```

## 测试要点

1. **功能测试**: 移动方向正确性、极点附近行为、相机跟随平滑度
2. **性能测试**: 大规模地形下的帧率、移动计算性能
3. **边界测试**: 极点附近万向锁、高速移动精度
4. **输入测试**: 键盘/手柄/触摸输入兼容性
