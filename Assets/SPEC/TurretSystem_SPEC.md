# 炮台系统 (Turret System) 规格说明书

## 模块概述

炮台系统是玩家操控的**唯一战斗单位**。炮台附着在球体表面，**自动搜索球内最近敌人**并开火。炮台本身不受伤害、不死亡。基础子弹免费发射，高级子弹消耗分数。

## 架构设计

### 核心组件

```
┌─────────────────────────────────────────────────────────┐
│                Turret (MonoBehaviour)                    │
├─────────────────────────────────────────────────────────┤
│  - 自动索敌 (球内敌人检测, Physics.OverlapSphere)        │
│  - 平滑瞄准 (Quaternion.Slerp)                           │
│  - 射击控制 (基础弹免费, 高级弹消耗分数)                  │
│  - 子弹切换 (数字键1-5快速切换)                           │
│  - 对象池 (避免 GC 压力)                                  │
└─────────────────────────────────────────────────────────┘
```

### 类图

```
┌─────────────┐         ┌─────────────────┐
│   Turret    │────────▶│ TurretLevelData │
│ (Controller)│         │(ScriptableObject)│
└──────┬──────┘         └─────────────────┘
       │
       ├────▶ BulletPool (ObjectPool<Bullet>)
       │
       └────▶ ScoreManager (分数消耗验证)
```

## 核心功能规格

### 1. 自动瞄准

- 炮台每帧（或降低频率）在**球体内部**搜索最近敌人
- 目标层: LayerMask `Enemy`
- 使用 `Physics.OverlapSphereNonAlloc` 避免 GC 分配
- 炮管平滑旋转瞄准目标（`Quaternion.Slerp`）
- 没有目标时炮管指向球心默认方向

### 2. 射击系统

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `fireRate` | 1.0/s | 每秒射击次数 |
| `damage` | 10 | 单发基础伤害 |
| `bulletCount` | 1 | 每次射击子弹数 |
| `detectionRange` | 球半径+5 | 索敌范围 |

**分数消耗规则**:
- 基础弹：免费发射，不消耗分数
- 高级弹：每发消耗对应分数，由 `ScoreManager` 扣除
- 分数不足时自动切换回基础弹
- 命中回馈分数 > 子弹消耗（净收益 +20% 以上）

### 3. 子弹切换

| 操作 | 按键 |
|------|------|
| 普通弹 | 1 |
| 穿透弹 | 2 |
| 追踪弹 | 3 |
| 散射弹 | 4 |
| 爆炸弹 | 5 |

已购买解锁的子弹才能切换，未购买的在商店中灰显。

### 4. 炮台升级（商店购买）

| 升级项 | 每级加成 | 最高等级 |
|--------|---------|:---:|
| 移动速度 | +10%/级 | 10 |
| 索敌范围 | +2/级 | 10 |
| 射速 | +0.2/s | 10 |
| 多发射击 | +1子弹 | 5 |

## 接口定义

```csharp
// 公共方法
public void EquipBullet(BulletType type);        // 切换子弹类型
public bool CanAffordBullet(BulletType type);     // 检查分数是否够
public void UpgradeAttribute(UpgradeType type);   // 升级属性
public float GetUpgradeCost(UpgradeType type);    // 获取升级价格

// 事件
public event Action<BulletType> OnBulletChanged;
public event Action OnFired;                      // 开火事件
public event Action<float> OnScoreSpent;          // 分数消耗事件
```

## 配置示例

```json
{
  "turret": {
    "moveSpeed": 30,
    "fireRate": 1.0,
    "damage": 10,
    "bulletCount": 1,
    "detectionRange": 15,
    "upgrades": {
      "moveSpeed": { "baseCost": 200, "increment": 0.1 },
      "fireRate": { "baseCost": 300, "increment": 0.2 },
      "detectionRange": { "baseCost": 150, "increment": 2 },
      "bulletCount": { "baseCost": 500, "increment": 1 }
    }
  }
}
```

## 依赖关系

```
TurretSystem
├── BulletSystem (射击)
├── ScoreManager (分数消耗)
├── EffectManager (开火/命中特效)
├── AudioManager (开火音效)
└── ObjectPool (子弹对象池)
```

## 测试要点

1. **功能测试**: 索敌逻辑、自动瞄准精度、子弹切换、分数不足降级
2. **性能测试**: 大量子弹时的对象池表现
3. **边界测试**: 无目标时行为、分数恰好用完

---

*对齐 GDD v3.0: 炮台自动索敌 + 高级弹消耗分数*
