# 子弹系统 (Bullet System) 规格说明书

## 模块概述

子弹系统管理所有抛射物（子弹）的生成、飞行、碰撞检测和伤害计算。系统支持 **7 种子弹类型**，每种有不同的分数消耗、行为特征和视觉效果。

## 设计原则

- **基础弹免费**，高级弹**消耗分数**（每发扣分）
- 命中回馈分数 > 子弹消耗，保证净收益
- 分数不足自动降级为基础弹
- 子弹类型**永久解锁**，可随时（免费）切换

## 子弹类型

| 类型 | 解锁价格 | 消耗(分/发) | 伤害倍率 | 特点 |
|------|:---:|:---:|:---:|------|
| **普通弹** | 初始 | 0 | ×1.0 | 直线飞行，无穿透 |
| **穿透弹** | 500 | 5 | ×0.8 | 穿透 3 个敌人 |
| **追踪弹** | 1000 | 8 | ×1.2 | 自动追踪最近目标 |
| **散射弹** | 1500 | 10 | ×0.6 | 每次 3 颗扇形子弹 |
| **爆炸弹** | 2000 | 15 | ×1.5 | 命中范围伤害(2m) |
| **毒液弹** | 3000 | 12 | ×0.7 | 持续伤害 3 秒 |
| **穿甲弹** | 5000 | 20 | ×1.0 | 穿透 5 个，无视距离衰减 |

## 架构设计

### 核心组件

```
┌─────────────────────────────────────────────────────────┐
│               Bullet (MonoBehaviour)                     │
├─────────────────────────────────────────────────────────┤
│  - 弹道计算 (直线/追踪)                                   │
│  - 碰撞检测 (射线检测)                                    │
│  - 伤害计算 (配置×倍率)                                   │
│  - 穿透/爆炸/毒液 特殊行为                                │
│  - 对象池回收 (超时/超距)                                 │
└─────────────────────────────────────────────────────────┘
```

### 类图

```
┌─────────────────┐         ┌─────────────────┐
│     Bullet      │◀───────▶│  BulletConfig   │
│  (Projectile)   │         │(ScriptableObject)│
└────────┬────────┘         └─────────────────┘
         │
         ▼
┌─────────────────┐
│   IDamageable   │
│   (Interface)   │
└─────────────────┘
```

## 核心功能规格

### 1. 子弹配置 (BulletConfig)

```csharp
[CreateAssetMenu(fileName = "BulletConfig", menuName = "Turret/Bullet Config")]
public class BulletConfig : ScriptableObject
{
    [Header("基础属性")]
    public string bulletName;
    public BulletType bulletType;
    public float damageMultiplier = 1f;
    public float speed = 20f;

    [Header("分数消耗")]
    public int scoreCostPerShot;       // 每发分数消耗, 0=免费
    public int unlockCost;             // 解锁价格

    [Header("特殊行为")]
    public int penetration;            // 穿透次数
    public bool isHoming;              // 是否追踪
    public float splashRadius;         // 爆炸范围(>0启用)
    public float dotDuration;          // 持续伤害时间(>0启用)
    public int multiShot;              // 散射数量(>1启用)
    public float multiShotAngle;       // 散射角度

    [Header("视觉效果")]
    public Color bulletColor;
    public GameObject trailPrefab;
    public GameObject hitEffectPrefab;
}
```

### 2. 弹道类型

**直线弹道** (普通弹、穿透弹、穿甲弹):
```csharp
transform.position += direction * speed * Time.deltaTime;
```

**追踪弹道** (追踪弹):
```csharp
Vector3 toTarget = (target.position - transform.position).normalized;
direction = Vector3.RotateTowards(direction, toTarget, homingStrength * dt, 0f);
transform.position += direction * speed * Time.deltaTime;
```

**散射** (散射弹):
```csharp
for (int i = 0; i < multiShot; i++)
{
    float angle = -spreadAngle/2 + i * spreadAngle/(multiShot-1);
    Quaternion spread = Quaternion.AngleAxis(angle, Vector3.up);
    Fire(direction * spread);
}
```

### 3. 特殊行为

**穿透**: 命中后不销毁，`remainingPenetration--`，继续飞行直到穿透次数耗尽

**爆炸**: 命中后检测半径内所有敌人，分别造成伤害

**毒液**: 命中后附加 `DoT` 效果，每秒扣血持续 N 秒

### 4. 分数消耗流程

```
开火 → ScoreManager.CanAfford(cost)?
         ├── 是 → 扣分 → 发射高级弹 → 命中回分
         └── 否 → 自动切基础弹 → 免费发射 → 提示用户
```

### 5. 通用子弹升级

| 升级项 | 每级加成 | 最高等级 | 影响所有子弹类型 |
|--------|---------|:---:|------|
| 伤害强化 | +20% | 10 | `damageMultiplier` 叠加 |
| 速度强化 | +15% | 8 | `speed` 叠加 |
| 子弹大小 | +10% | 5 | 视觉大小 + 碰撞体半径 |

## 配置示例

```json
{
  "bullets": {
    "normal": { "scoreCost": 0, "damage": 1.0, "penetration": 0 },
    "piercing": { "scoreCost": 5, "unlockCost": 500, "damage": 0.8, "penetration": 3 },
    "homing": { "scoreCost": 8, "unlockCost": 1000, "damage": 1.2, "isHoming": true },
    "scatter": { "scoreCost": 10, "unlockCost": 1500, "damage": 0.6, "multiShot": 3 },
    "explosive": { "scoreCost": 15, "unlockCost": 2000, "damage": 1.5, "splashRadius": 2.0 },
    "poison": { "scoreCost": 12, "unlockCost": 3000, "damage": 0.7, "dotDuration": 3.0 },
    "armorPiercing": { "scoreCost": 20, "unlockCost": 5000, "damage": 1.0, "penetration": 5 }
  },
  "upgrades": {
    "damage": { "baseCost": 400, "increment": 0.2, "maxLevel": 10 },
    "speed": { "baseCost": 300, "increment": 0.15, "maxLevel": 8 },
    "size": { "baseCost": 200, "increment": 0.1, "maxLevel": 5 }
  }
}
```

## 依赖关系

```
BulletSystem
├── ObjectPool (对象池)
├── EffectManager (命中特效/爆炸特效/毒液特效)
├── AudioManager (命中音效)
├── ScoreManager (分数消耗验证)
└── TurretSystem (发射者)
```

## 测试要点

1. **功能测试**: 各种子弹行为正确性、分数扣减、不足降级
2. **性能测试**: 散射弹×高射速下的对象池压力
3. **边界测试**: 穿透次数耗尽、追踪目标死亡、毒液叠加

---

*对齐 GDD v3.0: 7种子弹类型 + 分数消耗机制*
