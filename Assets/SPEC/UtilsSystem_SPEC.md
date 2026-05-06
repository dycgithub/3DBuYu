# 工具系统 (Utils System) 规格说明书

## 模块概述

工具系统提供了游戏开发中常用的通用功能模块，包括对象池、数学工具、扩展方法等。这些工具类设计为可复用、无依赖，可在多个项目中使用。

## 子系统列表

1. **ObjectPool** - 对象池系统
2. **SphericalCoordinates** - 球坐标数学工具
3. **ExtensionMethods** - 扩展方法集合

## ObjectPool 规格

### 核心组件

```
┌─────────────────────────────────────────────────────────────┐
│              ObjectPool<T> (Generic)                          │
├─────────────────────────────────────────────────────────────┤
│  泛型约束: where T : Component                              │
│                                                             │
│  - 预创建对象池                                             │
│  - 动态扩容机制                                             │
│  - 对象生命周期管理                                         │
│  - 自动回收与销毁                                           │
└─────────────────────────────────────────────────────────────┘
```

### 配置参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `defaultCapacity` | int | 10 | 默认池容量 |
| `maxSize` | int | 100 | 最大池大小 |
| `collectionCheck` | bool | true | 检查重复释放 |

### 核心方法

```csharp
/// <summary>
/// 创建对象池
/// </summary>
/// <param name="createFunc">创建函数</param>
/// <param name="actionOnGet">获取时回调</param>
/// <param name="actionOnRelease">释放时回调</param>
/// <param name="actionOnDestroy">销毁时回调</param>
/// <param name="collectionCheck">检查重复释放</param>
/// <param name="defaultCapacity">默认容量</param>
/// <param name="maxSize">最大大小</param>
public ObjectPool(
    Func<T> createFunc,
    Action<T> actionOnGet = null,
    Action<T> actionOnRelease = null,
    Action<T> actionOnDestroy = null,
    bool collectionCheck = true,
    int defaultCapacity = 10,
    int maxSize = 100
)

/// <summary>
/// 从池中获取对象
/// </summary>
public T Get()

/// <summary>
/// 将对象归还到池
/// </summary>
public void Release(T element)

/// <summary>
/// 清空对象池
/// </summary>
public void Clear()
```

### 使用示例

```csharp
// 子弹对象池
private ObjectPool<Bullet> bulletPool;

void Start()
{
    bulletPool = new ObjectPool<Bullet>(
        createFunc: CreateBullet,
        actionOnGet: ResetBullet,
        actionOnRelease: DeactivateBullet,
        actionOnDestroy: DestroyBullet,
        defaultCapacity: 20,
        maxSize: 100
    );
}

Bullet CreateBullet()
{
    Bullet bullet = Instantiate(bulletPrefab).GetComponent<Bullet>();
    bullet.SetPool(bulletPool);
    return bullet;
}

void ResetBullet(Bullet bullet)
{
    bullet.gameObject.SetActive(true);
    bullet.ResetState();
}

void DeactivateBullet(Bullet bullet)
{
    bullet.gameObject.SetActive(false);
}

void DestroyBullet(Bullet bullet)
{
    Destroy(bullet.gameObject);
}

// 使用
void Fire()
{
    Bullet bullet = bulletPool.Get();
    bullet.Fire(direction);
}
```

## SphericalCoordinates 规格

### 核心功能

提供球坐标系与笛卡尔坐标系的转换，以及球面上的几何计算。

### 静态方法

```csharp
/// <summary>
/// 球坐标转换为笛卡尔坐标
/// </summary>
/// <param name="longitude">经度 (度)</param>
/// <param name="latitude">纬度 (度)</param>
/// <param name="radius">半径</param>
/// <returns>笛卡尔坐标</returns>
public static Vector3 SphericalToCartesian(float longitude, float latitude, float radius)
{
    float lonRad = longitude * Mathf.Deg2Rad;
    float latRad = latitude * Mathf.Deg2Rad;

    float x = radius * Mathf.Cos(latRad) * Mathf.Cos(lonRad);
    float y = radius * Mathf.Sin(latRad);
    float z = radius * Mathf.Cos(latRad) * Mathf.Sin(lonRad);

    return new Vector3(x, y, z);
}

/// <summary>
/// 笛卡尔坐标转换为球坐标
/// </summary>
/// <param name="cartesian">笛卡尔坐标</param>
/// <param name="longitude">输出经度</param>
/// <param name="latitude">输出纬度</param>
/// <param name="radius">输出半径</param>
public static void CartesianToSpherical(Vector3 cartesian, out float longitude, out float latitude, out float radius)
{
    radius = cartesian.magnitude;
    longitude = Mathf.Atan2(cartesian.z, cartesian.x) * Mathf.Rad2Deg;
    latitude = Mathf.Asin(cartesian.y / radius) * Mathf.Rad2Deg;
}

/// <summary>
/// 计算球面上两点间的大圆距离
/// </summary>
public static float GreatCircleDistance(Vector3 point1, Vector3 point2, float radius)
{
    float dot = Vector3.Dot(point1.normalized, point2.normalized);
    dot = Mathf.Clamp(dot, -1f, 1f);
    float angle = Mathf.Acos(dot);
    return radius * angle;
}

/// <summary>
/// 球面插值
/// </summary>
public static Vector3 SphericalLerp(Vector3 from, Vector3 to, float t)
{
    from = from.normalized;
    to = to.normalized;

    float dot = Vector3.Dot(from, to);
    dot = Mathf.Clamp(dot, -1f, 1f);

    float theta = Mathf.Acos(dot) * t;
    Vector3 relative = to - from * dot;
    relative = relative.normalized;

    return from * Mathf.Cos(theta) + relative * Mathf.Sin(theta);
}
```

## 依赖关系

```
UtilsSystem
├── UnityEngine (核心)
└── System.Collections.Generic (集合)
```

## 测试要点

1. **对象池测试**: 并发获取/释放、边界条件、内存泄漏
2. **球坐标测试**: 转换精度、边界情况(极点)、性能
