# 工具系统 (Utils System) 规格说明书

## 模块概述

工具系统提供通用功能模块：对象池、球坐标数学工具。设计为无依赖、可复用。

## ObjectPool

泛型对象池，支持 `Component` 类型 (`ObjectPool<T>`) 和普通类 (`ObjectPoolSimple<T>`)。

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `defaultCapacity` | 10 | 默认池容量 |
| `maxSize` | 100 | 最大池大小 |

用于: 子弹、特效、鱼群实体

## SphericalCoordinates

球面坐标数学工具（`SphereMovement` 命名空间，位于 `SphericalCoordinates.cs`）。

实际 API（与代码一致）：

```csharp
// 笛卡尔 → 球面 (返回 Vector2, x=经度, y=纬度, 弧度)
public static Vector2 FromCartesian(Vector3 cartesian);

// 球面 → 笛卡尔 (单位球面)
public static Vector3 ToCartesian(Vector2 spherical);

// 大圆距离 (通过 SphereSurface.GetGreatCircleDistance)
```

**注意**: 使用时经度为弧度，纬度范围 [-π/2, π/2]。

## 依赖关系

```
UtilsSystem
├── UnityEngine
└── System.Collections.Generic
```

---

*对齐 GDD v3.0: 修正 SphericalCoordinates API 描述*
