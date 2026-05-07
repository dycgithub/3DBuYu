# 游戏系统 (Game System) 规格说明书

## 模块概述

游戏系统是中央控制器，管理游戏状态、**分数资源**、**倒计时**、**Buff系统**和**关卡流程**。不同于传统塔防的"金币/经验"体系，本游戏采用**分数即资源**模型 — 分数既是通关指标也是消耗货币。

## 子系统列表

1. **GameManager** — 游戏状态机
2. **ScoreManager** — 分数管理（获取/消耗/门槛检测）
3. **TimerSystem** — 倒计时系统
4. **BuffManager** — Buff 叠加与效果管理
5. **LevelManager** — 关卡配置与难度曲线
6. **SaveSystem** — 永久进度、解锁、设置存档

---

## 1. GameManager — 游戏状态机

### 状态流转

```
主菜单 ──→ 游戏中 ──→ 通关结算 ──→ 下一关
              │          │
              │          └── 分数 ≥ 门槛 → 晋级
              │
              ├── Tab → 商店(时间不暂停)
              │
              └── 时间到 → 分数 < 门槛 → 失败结算
```

### 游戏状态

| 状态 | 说明 |
|------|------|
| `Menu` | 主菜单 |
| `Playing` | 核心循环 |
| `Shop` | 商店（时间不暂停） |
| `Lottery` | 抽奖轮盘（时间暂停） |
| `LevelComplete` | 通关结算 |
| `GameOver` | 失败结算 |

### 事件

```csharp
public event Action<GameState> OnStateChanged;
public event Action<int> OnLevelStarted;
public event Action<int, bool> OnLevelEnded;    // (关卡号, 是否达标)
public event Action OnGameOver;
```

---

## 2. ScoreManager — 分数=资源

### 核心设计

分数是**双重身份**:
- **积分**: 通关门槛指标
- **货币**: 高级子弹消耗 + 商店购买

### 接口

```csharp
public int CurrentScore { get; }
public int TotalScoreEarned { get; }  // 历史总分

public void AddScore(int amount, string source = "");
public bool SpendScore(int amount, string source = "");  // 返回是否成功
public bool CanAfford(int cost);
```

### 获取来源

| 来源 | 基础分 | 倍率 |
|------|:---:|------|
| 击杀普通鱼 | 10 | ×积分倍率 |
| 击杀快速鱼 | 25 | ×积分倍率 |
| 击杀飞鱼 | 35 | ×积分倍率 |
| 击杀坦克鱼 | 50 | ×积分倍率 |
| 击杀黄金鱼 | 200 | ×积分倍率 |
| 击杀BOSS鱼 | 1000 | ×积分倍率 |
| 每发命中 | +2 | 鼓励精准 |
| 分数包掉落 | +50 | — |

### 消耗出口

| 消耗 | 说明 |
|------|------|
| 高级弹发射 | 5~20分/发（取决于子弹类型） |
| 解锁子弹 | 500~5000分（永久） |
| 子弹升级 | 200~1600分/级 |
| 炮台升级 | 150~1000分/级 |
| 商店Buff | 300~3000分 |

---

## 3. TimerSystem — 倒计时

### 核心设计

每关有固定倒计时。时间归零 → 检查分数是否达标。

### 参数

| 关卡 | 限时(秒) | 门槛分数 |
|:---:|:---:|:---:|
| 1 | 120 | 500 |
| 2 | 150 | 1200 |
| 3 | 180 | 2500 |
| 4 | 200 | 4000 |
| 5 (BOSS) | 240 | 6000 |
| 6 | 240 | 9000 |
| 7 | 270 | 13000 |
| 8 | 300 | 18000 |
| 9 | 330 | 25000 |
| 10 | 360 | 35000 |

### 接口

```csharp
public float RemainingTime { get; }
public bool IsTimeUp { get; }
public bool IsPaused { get; }

public void StartTimer(float seconds);
public void AddTime(float seconds);    // +时间Buff
public void Pause();
public void Resume();

public event Action<float> OnTimeChanged;    // 每秒
public event Action OnTimeUp;                // 时间到
```

### 时间Buff

- **击杀掉落**: 时间包 +10秒（概率5%）
- **商店购买**: +30秒（500分）

---

## 4. BuffManager — Buff系统

### Buff类型

| Buff | 效果 | 默认持续 | 来源 |
|------|------|:---:|------|
| **穿透** | 子弹穿透数+2 | 15s | 掉落/商店 |
| **加时间** | 限时+30秒 | 即时 | 掉落/商店 |
| **双倍得分** | 所有得分×2 | 20s | 掉落/商店 |
| **射速提升** | 射速×1.5 | 20s | 掉落 |
| **伤害提升** | 伤害×1.5 | 15s | 掉落 |
| **冰冻** | 鱼群减速50% | 10s | 掉落/商店 |
| **磁铁** | 自动吸取掉落 | 25s | 商店 |

### 获取方式

| 方式 | 说明 |
|------|------|
| **击杀掉落** | 8%概率掉落随机临时Buff |
| **商店常驻** | 消耗分数购买，持续整关 |
| **商店临时** | 消耗分数购买，定时生效 |
| **黄金鱼抽奖** | 击杀黄金鱼触发轮盘 |

### Buff叠加规则

- 同类型Buff: 时间叠加（取最长），效果取最强
- 不同类型Buff: 可同时生效
- 双倍得分 + 伤害提升: 乘法叠加

### 接口

```csharp
public void ActivateBuff(BuffType type, float duration, bool permanent = false);
public void DeactivateBuff(BuffType type);
public bool HasBuff(BuffType type);
public float GetBuffMultiplier(BuffType type);

public event Action<BuffType, bool> OnBuffChanged;
```

---

## 5. LevelManager — 关卡管理

### 关卡配置

| 关卡 | 球半径 | 鱼数 | 鱼类型 | 门槛 | 限时 | BOSS |
|:---:|:---:|:---:|------|:---:|:---:|:---:|
| 1 | 10 | 25 | 普通鱼 | 500 | 120s | — |
| 2 | 12 | 30 | +快速鱼 | 1200 | 150s | — |
| 3 | 15 | 35 | +飞鱼 | 2500 | 180s | — |
| 4 | 18 | 40 | +坦克鱼 | 4000 | 200s | — |
| 5 | 20 | 50 | 全类型 | 6000 | 240s | BOSS |
| 6-9 | 22-28 | 55-70 | 全类型混出 | 渐进 | 渐进 | — |
| 10 | 30 | 80 | 全类型 | 35000 | 360s | 最终BOSS |

### 接口

```csharp
public int CurrentLevel { get; }
public LevelConfig GetCurrentLevelConfig();
public void StartLevel(int level);
public bool CheckLevelComplete();  // 时间到+分数达标
public void AdvanceToNextLevel();
```

---

## 6. SaveSystem — 存档

### 存档内容

| 数据 | 说明 |
|------|------|
| 已解锁子弹类型 | 永久解锁记录 |
| 子弹升级等级 | 伤害/速度/大小 |
| 炮台升级等级 | 移速/射程/射速/多发射击 |
| 最高通关关卡 | 解锁跳关 |
| 设置 | 音量/画面 |

存档使用 JSON 序列化，路径: `Application.persistentDataPath/`

---

## 数据流

```
LevelManager (关卡配置)
      │
      ├──→ SphereSurface.radius
      ├──→ EnemySpawnManager (鱼群配置)
      └──→ TimerSystem (倒计时)
                │
                ▼
          ┌── 时间到 ──→ CheckScore()
          │                │
          │           ┌────┴────┐
          │       达标(Buff)  未达标
          │           │         │
          │       下一关    GameOver
          │
Turret → 射击 → Bullet → 鱼
  │         │        │
  │    ScoreManager   │
  │    (扣分检查)     │
  │         │         │
  │         └────┬────┘
  │              │
  ▼              ▼
ShopUI ←── ScoreManager (加分+掉落)
  │              │
  │              ▼
  │         BuffManager
  │              │
  └──────┬───────┘
         │
    GameManager (状态控制)
```

## 依赖关系

```
GameSystem
├── ScoreManager
│   └── SaveSystem (解锁永久化)
├── TimerSystem
│   └── BuffManager (时间Buff)
├── BuffManager
│   └── TurretSystem / BulletSystem (效果施加)
├── LevelManager
│   ├── TimerSystem
│   └── ScoreManager
├── SaveSystem
│   └── JSON + File I/O
└── GameManager (顶层协调)
```

## 测试要点

1. **功能测试**: 分数增减、消耗不足降级、倒计时归零判定、Buff叠加
2. **性能测试**: 高频分数变动、多Buff同时生效
3. **边界测试**: 分数恰好等于门槛、最后一秒达标、分数溢出

---

*对齐 GDD v3.0: 分数=资源 + 限时通关 + Buff系统*
