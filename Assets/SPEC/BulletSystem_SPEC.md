# 子弹系统 (Bullet System) 规格说明书

## 模块概述

子弹系统负责管理游戏中的所有抛射物，包括子弹的生成、飞行、碰撞检测和伤害计算。系统支持多种子弹类型和高级特性如穿透、追踪等。

## 架构设计

### 核心组件

```
┌─────────────────────────────────────────┐
│           Bullet (MonoBehaviour)          │
├─────────────────────────────────────────┤
│  - 弹道计算 (直线/追踪/抛物线)            │
│  - 碰撞检测 (射线检测/触发器)             │
│  - 伤害计算 (基于距离/穿透)               │
│  - 生命周期管理 (超时/最大距离)           │
└─────────────────────────────────────────┘
```

### 类图

```
┌─────────────────┐         ┌─────────────────┐
│     Bullet      │◀───────▶│  BulletConfig   │
│  (Projectile)   │         │(ScriptableObject)│
└─────────────────┘         └─────────────────┘
         │
         ▼
┌─────────────────┐
│   IDamageable   │
│   (Interface)   │
└─────────────────┘
```

## 核心功能规格

### 1. 子弹配置 (BulletConfig)

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `bulletName` | string | "Default" | 子弹名称 |
| `damage` | float | 10f | 基础伤害 |
| `speed` | float | 20f | 飞行速度 |
| `lifetime` | float | 3f | 最大存活时间 |
| `maxDistance` | float | 100f | 最大飞行距离 |
| `penetration` | int | 0 | 穿透次数(0=不穿透) |
| `isHoming` | bool | false | 是否追踪 |
| `homingStrength` | float | 5f | 追踪强度 |

**ScriptableObject 定义**:
```csharp
[CreateAssetMenu(fileName = "BulletConfig", menuName = "Turret/Bullet Config")]
public class BulletConfig : ScriptableObject
{
    [Header("基础属性")]
    public string bulletName = "Default";
    public float damage = 10f;
    public float speed = 20f;

    [Header("生命周期")]
    public float lifetime = 3f;
    public float maxDistance = 100f;

    [Header("穿透设置")]
    public int penetration = 0; // 0 = 不穿透, 1+ = 穿透次数
    public bool penetrateWalls = false;

    [Header("追踪设置")]
    public bool isHoming = false;
    public float homingStrength = 5f;
    public float homingAngle = 45f; // 最大追踪角度

    [Header("视觉效果")]
    public GameObject modelPrefab;
    public ParticleSystem trailEffect;
    public ParticleSystem hitEffect;
}
```

### 2. 子弹行为 (Bullet)

| 属性 | 类型 | 说明 |
|------|------|------|
| `config` | BulletConfig | 子弹配置 |
| `owner` | Transform | 发射者 |
| `target` | Transform | 追踪目标 |
| `startPosition` | Vector3 | 起始位置 |
| `penetratedTargets` | List<Collider> | 已穿透的目标 |

**核心方法**:

```csharp
/// <summary>
/// 初始化子弹
/// </summary>
public void Initialize(BulletConfig config, Transform owner, Transform target = null)

/// <summary>
/// 发射子弹
/// </summary>
public void Fire(Vector3 direction)

/// <summary>
/// 处理命中
/// </summary>
private void OnHit(Collider target, Vector3 hitPoint, Vector3 hitNormal)

/// <summary>
/// 处理穿透
/// </summary>
private void HandlePenetration(Collider target)

/// <summary>
/// 销毁/回收子弹
/// </summary>
private void DestroyBullet()
```

### 3. 弹道计算

#### 直线弹道
```csharp
// 基础直线飞行
transform.position += direction * speed * Time.deltaTime;
```

#### 追踪弹道
```csharp
// 朝向目标转向
if (target != null)
{
    Vector3 toTarget = (target.position - transform.position).normalized;
    direction = Vector3.RotateTowards(
        direction,
        toTarget,
        homingStrength * Time.deltaTime,
        0f
    );
}
transform.position += direction * speed * Time.deltaTime;
```

### 4. 碰撞检测

**射线检测方式** (推荐用于高速子弹):
```csharp
// 基于距离的射线检测，防止穿墙
float distanceThisFrame = speed * Time.deltaTime;
Ray ray = new Ray(transform.position, direction);
RaycastHit hit;

if (Physics.Raycast(ray, out hit, distanceThisFrame, collisionLayer))
{
    OnHit(hit.collider, hit.point, hit.normal);
}
```

**触发器方式** (用于低速或大型子弹):
```csharp
void OnTriggerEnter(Collider other)
{
    if (IsValidTarget(other))
    {
        OnHit(other, transform.position, -transform.forward);
    }
}
```

### 5. 穿透机制

```csharp
private int remainingPenetration;
private List<Collider> penetratedTargets = new List<Collider>();

private void OnHit(Collider target, Vector3 hitPoint, Vector3 hitNormal)
{
    // 检查是否已穿透此目标
    if (penetratedTargets.Contains(target)) return;

    // 应用伤害
    ApplyDamage(target);

    // 处理穿透
    if (remainingPenetration > 0)
    {
        remainingPenetration--;
        penetratedTargets.Add(target);
        // 继续飞行
        PlayHitEffect(hitPoint, hitNormal);
    }
    else
    {
        DestroyBullet();
    }
}
```

## 接口定义

### IDamageable 接口

```csharp
public interface IDamageable
{
    void TakeDamage(float damage, Transform attacker = null);
    float CurrentHealth { get; }
    bool IsDead { get; }
}
```

## 性能优化

### 对象池

```csharp
public class BulletPool : MonoBehaviour
{
    private ObjectPool<Bullet> pool;

    void Awake()
    {
        pool = new ObjectPool<Bullet>(
            CreateBullet,
            OnGetBullet,
            OnReleaseBullet,
            OnDestroyBullet,
            defaultCapacity: 50,
            maxSize: 200
        );
    }

    public Bullet Get(BulletConfig config, Transform owner)
    {
        Bullet bullet = pool.Get();
        bullet.Initialize(config, owner);
        return bullet;
    }

    public void Release(Bullet bullet)
    {
        pool.Release(bullet);
    }
}
```

### 优化技巧

1. **射线检测优化**: 使用 layer mask 限制检测层
2. **距离计算优化**: 使用平方距离比较避免开方运算
3. **分配优化**: 缓存 transform 引用，使用对象池
4. **碰撞优化**: 高速子弹使用射线检测而非刚体

## 配置示例

```json
{
  "bulletTypes": [
    {
      "name": "Standard",
      "damage": 10,
      "speed": 20,
      "penetration": 0,
      "isHoming": false
    },
    {
      "name": "Sniper",
      "damage": 50,
      "speed": 50,
      "penetration": 3,
      "isHoming": false
    },
    {
      "name": "Homing",
      "damage": 15,
      "speed": 15,
      "penetration": 0,
      "isHoming": true,
      "homingStrength": 3
    }
  ]
}
```

## 依赖关系

```
BulletSystem
├── Utils/ObjectPool (对象池)
├── Effects/EffectManager (命中特效)
└── TurretSystem (发射者)
```

## 测试要点

1. **功能测试**: 弹道准确性、伤害计算、穿透逻辑
2. **性能测试**: 大量子弹时的帧率、内存分配
3. **边界测试**: 最大穿透次数、目标同时死亡处理
4. **网络测试**: 子弹同步(如有多人模式)
