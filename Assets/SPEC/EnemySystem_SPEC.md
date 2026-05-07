# 敌人系统 (Enemy System) 规格说明书

## 模块概述

敌人系统管理**球体内部游动的怪物鱼群**。鱼群使用 boids 群游算法在球内三维空间自由游动，产生自然的聚集、分离、巡游行为。部分鱼会主动靠近球壳（炮台方向）。击杀鱼群获得分数，稀有鱼种触发特殊事件。

## 架构设计

### 核心组件

```
┌─────────────────────────────────────────────────────────────┐
│                 EnemyBase (Abstract)                         │
├─────────────────────────────────────────────────────────────┤
│  - 生命系统 (Health/Damage/Death)                            │
│  - 掉落系统 (分数/Buff道具/时间包)                            │
│  - 球内约束 (超出球体折返)                                   │
└─────────────────────────────────────────────────────────────┘
         │
         ├── EnemyNormal     (普通鱼, boids主体)
         ├── EnemyFast       (快速鱼, 高速巡游)
         ├── EnemyTank       (坦克鱼, 大型慢速)
         ├── EnemyFlying     (飞鱼, 上下波动)
         ├── GoldenFish      (黄金鱼, 稀有, 触发抽奖)
         └── BossFish        (BOSS鱼, 关卡BOSS)

┌─────────────────────────────────────────────────────────┐
│            Flocking System (Boids算法)                   │
├─────────────────────────────────────────────────────────┤
│  - globalFlock: 鱼群管理器, 生成/目标/边界               │
│  - flock: 单鱼行为, 分离/聚合/对齐                       │
└─────────────────────────────────────────────────────────┘
```

## 核心功能规格

### 1. 敌人类型

| 类型 | 血量 | 速度 | 击杀分 | 行为特征 |
|------|------|------|:---:|---------|
| **普通鱼** | 30 | 2.0 | 10 | 标准 boids 行为，占鱼群 60%+ |
| **快速鱼** | 15 | 5.0 | 25 | 高速巡游，偶尔冲刺靠近球壳 |
| **坦克鱼** | 120 | 1.2 | 50 | 大型慢速，吸引火力保护小鱼 |
| **飞鱼** | 40 | 3.5 | 35 | 上下波动飞行，更难瞄准 |
| **黄金鱼** | 80 | 2.5 | 200 | 稀有(3%)，击杀触发抽奖轮盘 |
| **BOSS鱼** | 500 | 1.0 | 1000 | 关卡BOSS，巨大体型 |

### 2. 鱼群行为 (Boids 算法)

三条核心规则：

| 规则 | 权重 | 说明 |
|------|------|------|
| **分离** | 1.5 | 避免与邻近鱼碰撞 (距离 < 1.0m) |
| **聚合** | 1.0 | 向邻近鱼群中心靠拢 (距离 < 3.0m) |
| **对齐** | 0.8 | 匹配邻近鱼的平均速度方向 |

额外规则：
- **球内约束**: 鱼的位置始终限制在球体半径内，超出时转向球心
- **炮台趋向**: 15% 的鱼在炮台附近游动（增加可射击目标）
- **BOSS光环**: BOSS鱼周围聚集保护性小鱼

### 3. 掉落系统

击杀敌人后掉落：

| 掉落类型 | 概率 | 内容 |
|----------|:---:|------|
| 分数包 | 15% | +50分 |
| Buff道具(临时) | 8% | 随机临时Buff(15-25s) |
| 时间包 | 5% | +10秒 |
| 无掉落 | 72% | — |

### 4. 生成规则

| 参数 | 初始值 | 说明 |
|------|--------|------|
| `totalFishCount` | 25 | 场上总鱼数 |
| `fishCountPerLevel` | +5/关 | 每关递增 |
| `maxFishCount` | 150 | 场上最大鱼数 |
| `spawnInterval` | 3s | 补充间隔 |
| `spawnArea` | 球内随机 | 球体内部任意位置 |
| `goldenFishRate` | 3% | 黄金鱼出现概率 |
| `bossLevels` | 5, 10 | BOSS关卡 |

### 5. 难度曲线

| 关卡 | 活跃鱼类型 | 血量倍率 | 积分倍率 |
|:---:|-----------|:---:|:---:|
| 1 | 普通鱼 | ×1.0 | ×1.0 |
| 2 | 普通鱼 + 快速鱼(15%) | ×1.2 | ×1.1 |
| 3 | 普通鱼 + 快速鱼 + 飞鱼(10%) | ×1.5 | ×1.2 |
| 4 | 全类型 + 坦克鱼(10%) | ×2.0 | ×1.4 |
| 5 | BOSS关 | ×2.5 | ×1.5 |
| 6-9 | 全类型混出 | ×2.8→4.0 | ×1.6→2.0 |
| 10 | 最终BOSS | ×5.0 | ×3.0 |

## 接口定义

```csharp
public virtual void TakeDamage(float damage, BulletType bulletType);
public virtual void Die();
protected virtual void DropItems();           // 分数+道具
public virtual void ApplyBuff(BuffType buff);  // 减速/冰冻

public static event Action<EnemyBase> OnEnemyDied;
public static event Action<EnemyBase> OnGoldenFishKilled;  // 触发抽奖
public static event Action<EnemyBase> OnBossKilled;
```

## 性能优化

- Boids 计算使用 ECS/Jobs 优化（鱼群 > 100 时必需）
- 对象池管理所有鱼实例
- LOD: 远距离鱼降低 boids 计算频率
- 使用 `Physics.OverlapSphereNonAlloc` 避免分配

## 配置示例

```json
{
  "enemies": {
    "normal":    { "hp": 30, "speed": 2.0, "score": 10 },
    "fast":      { "hp": 15, "speed": 5.0, "score": 25 },
    "tank":      { "hp": 120, "speed": 1.2, "score": 50 },
    "flying":    { "hp": 40, "speed": 3.5, "score": 35 },
    "golden":    { "hp": 80, "speed": 2.5, "score": 200, "spawnRate": 0.03 },
    "boss":      { "hp": 500, "speed": 1.0, "score": 1000 }
  },
  "flocking": {
    "separationWeight": 1.5,
    "cohesionWeight": 1.0,
    "alignmentWeight": 0.8,
    "neighborRadius": 3.0,
    "separationRadius": 1.0
  }
}
```

## 依赖关系

```
EnemySystem
├── Flocking/ (boids 群游)
├── EffectManager (死亡特效/黄金鱼特效)
├── ScoreManager (击杀积分)
├── BuffManager (掉落Buff)
├── TimerSystem (掉落时间包)
├── ObjectPool (鱼对象池)
└── AudioManager (鱼群音效)
```

## 测试要点

1. **功能测试**: boids 行为正确性、球内约束、掉落概率
2. **性能测试**: 150条鱼 boids 计算帧率、对象池压力
3. **边界测试**: 球体边界行为、所有鱼同时死亡、黄金鱼触发

---

*对齐 GDD v3.0: 球内鱼群 + boids + 黄金鱼抽奖*
