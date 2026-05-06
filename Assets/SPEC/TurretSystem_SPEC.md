# 炮塔系统 (Turret System) 规格说明书

## 模块概述

炮塔系统是游戏的核心战斗系统，负责管理炮塔的瞄准、射击和升级功能。炮塔会自动搜索并攻击范围内的敌人。

## 架构设计

### 核心组件

```
┌─────────────────────────────────────────┐
│           Turret (MonoBehaviour)        │
├─────────────────────────────────────────┤
│  - 目标检测 (球形射线检测)              │
│  - 平滑旋转 (Quaternion.Slerp)          │
│  - 射击控制 (协程/计时器)               │
│  - 升级系统 (级别数据切换)              │
└─────────────────────────────────────────┘
```

### 类图

```
┌─────────────┐         ┌─────────────────┐
│   Turret    │────────▶│ TurretLevelData │
│  (Controller)│         │  (ScriptableObject)│
└─────────────┘         └─────────────────┘
        │
        ▼
┌─────────────┐         ┌─────────────────┐
│ BulletPool  │◀───────▶│     Bullet      │
│  (ObjectPool)│         │  (Projectile)   │
└─────────────┘         └─────────────────┘
```

## 核心功能规格

### 1. 目标检测

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `detectionRange` | float | 15f | 检测半径 |
| `targetLayer` | LayerMask | Enemy | 目标层 |
| `targetTag` | string | "Enemy" | 目标标签 |

**检测逻辑**:
```csharp
// 使用 Physics.OverlapSphere 进行范围检测
Collider[] enemiesInRange = Physics.OverlapSphere(transform.position, detectionRange, targetLayer);
// 选择距离最近的敌人作为目标
```

### 2. 旋转瞄准

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `rotationSpeed` | float | 5f | 旋转速度 |
| `aimThreshold` | float | 5f | 瞄准阈值(角度) |

**旋转逻辑**:
```csharp
// 使用 Quaternion.Slerp 实现平滑旋转
Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
```

### 3. 射击系统

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `fireRate` | float | 1f | 每秒射击次数 |
| `firePoint` | Transform | null | 子弹生成点 |
| `bulletPrefab` | GameObject | null | 子弹预制体 |

**射击逻辑**:
```csharp
// 使用计时器控制射击间隔
if (Time.time >= nextFireTime)
{
    Fire();
    nextFireTime = Time.time + 1f / fireRate;
}
```

### 4. 升级系统

| 属性 | 类型 | 说明 |
|------|------|------|
| `levelDataArray` | TurretLevelData[] | 各级数据配置 |
| `currentLevel` | int | 当前等级(从1开始) |
| `upgradeCost` | int | 升级花费 |

**升级数据 (TurretLevelData)**:
```csharp
[CreateAssetMenu(fileName = "TurretLevelData", menuName = "Turret/Level Data")]
public class TurretLevelData : ScriptableObject
{
    public int level;                    // 等级
    public float damage;                 // 伤害
    public float range;                  // 射程
    public float fireRate;               // 射速
    public GameObject turretModel;       // 炮塔模型
    public Material turretMaterial;      // 炮塔材质
    public ParticleSystem upgradeEffect; // 升级特效
}
```

## 接口定义

### 公共方法

```csharp
/// <summary>
/// 升级炮塔
/// </summary>
/// <param name="playerCoins">玩家金币数</param>
/// <returns>是否升级成功</returns>
public bool TryUpgrade(int playerCoins)

/// <summary>
/// 获取当前升级花费
/// </summary>
public int GetUpgradeCost()

/// <summary>
/// 出售炮塔
/// </summary>
/// <returns>返还金币数</returns>
public int Sell()

/// <summary>
/// 设置目标
/// </summary>
public void SetTarget(Transform target)
```

### 事件

```csharp
/// <summary>
/// 炮塔升级事件
/// </summary>
public event Action<int> OnTurretUpgraded;

/// <summary>
/// 炮塔出售事件
/// </summary>
public event Action OnTurretSold;

/// <summary>
/// 炮塔开火事件
/// </summary>
public event Action OnTurretFired;
```

## 性能优化

### 对象池

使用 `ObjectPool<T>` 管理子弹对象，避免频繁的 Instantiate/Destroy 操作。

```csharp
private ObjectPool<Bullet> bulletPool;

void Awake()
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
```

### 检测优化

- 使用非分配式检测: `Physics.OverlapSphereNonAlloc`
- 分层检测: 只在Enemy层检测
- 降低检测频率: 不需要每帧检测

### LOD (Level of Detail)

- 远距离炮塔降低旋转/检测频率
- 远距离敌人使用简化碰撞体

## 配置示例

```json
{
  "turretLevels": [
    {
      "level": 1,
      "damage": 10,
      "range": 10,
      "fireRate": 1.0,
      "upgradeCost": 100
    },
    {
      "level": 2,
      "damage": 20,
      "range": 12,
      "fireRate": 1.2,
      "upgradeCost": 250
    },
    {
      "level": 3,
      "damage": 35,
      "range": 15,
      "fireRate": 1.5,
      "upgradeCost": 0
    }
  ]
}
```

## 依赖关系

```
TurretSystem
├── BulletSystem (射击)
├── EffectSystem (特效)
├── ResourceManager (升级花费)
└── Utils/ObjectPool (对象池)
```

## 测试要点

1. **功能测试**
   - 目标检测范围和角度
   - 旋转平滑度和精度
   - 射击间隔和伤害
   - 升级属性和花费

2. **性能测试**
   - 大量炮塔时的帧率
   - 对象池的内存使用
   - 检测频率对性能的影响

3. **边界测试**
   - 目标死亡/销毁处理
   - 升级最大值处理
   - 没有有效目标时的行为
