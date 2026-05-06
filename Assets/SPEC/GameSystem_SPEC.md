# 游戏系统 (Game System) 规格说明书

## 模块概述

游戏系统是游戏的中央控制器，负责管理游戏状态、流程控制、资源管理和存档系统。它协调各个子系统，确保游戏逻辑的正确执行。

## 子系统列表

1. **GameManager** - 游戏状态管理
2. **ResourceManager** - 资源管理（金币、经验、宝石等）
3. **SaveSystem** - 存档系统
4. **DropManager** - 掉落管理

## GameManager 规格

### 核心功能

```
┌─────────────────────────────────────────────────────────┐
│                     GameManager                         │
├─────────────────────────────────────────────────────────┤
│  - 游戏状态机 (Menu/Playing/Paused/GameOver/Victory)   │
│  - 难度管理 (Easy/Normal/Hard/Nightmare)               │
│  - 场景管理 (加载/切换/进度)                            │
│  - 事件系统 (游戏开始/暂停/结束/胜利)                   │
│  - 统计追踪 (游戏时间/击杀数/波数)                       │
└─────────────────────────────────────────────────────────┘
```

### 游戏状态

| 状态 | 说明 | 允许操作 |
|------|------|----------|
| `Menu` | 主菜单状态 | 开始游戏、设置、退出 |
| `Playing` | 游戏进行中 | 移动、攻击、暂停 |
| `Paused` | 暂停状态 | 继续、重试、返回菜单 |
| `GameOver` | 游戏结束 | 重试、返回菜单 |
| `Victory` | 胜利状态 | 下一关、返回菜单 |

### 难度配置

| 难度 | 敌人血量倍率 | 敌人伤害倍率 | 敌人数量倍率 | 奖励倍率 |
|------|-------------|-------------|-------------|---------|
| `Easy` | 0.7x | 0.7x | 0.8x | 0.8x |
| `Normal` | 1.0x | 1.0x | 1.0x | 1.0x |
| `Hard` | 1.3x | 1.3x | 1.2x | 1.2x |
| `Nightmare` | 2.0x | 1.8x | 1.5x | 2.0x |

### 事件接口

```csharp
/// <summary>
/// 游戏开始事件
/// </summary>
public event Action OnGameStarted;

/// <summary>
/// 游戏暂停事件
/// </summary>
public event Action OnGamePaused;

/// <summary>
/// 游戏恢复事件
/// </summary>
public event Action OnGameResumed;

/// <summary>
/// 游戏结束事件
/// </summary>
public event Action OnGameOver;

/// <summary>
/// 游戏胜利事件
/// </summary>
public event Action OnVictory;

/// <summary>
/// 难度改变事件
/// </summary>
public event Action<Difficulty> OnDifficultyChanged;

/// <summary>
/// 统计更新事件
/// </summary>
public event Action<GameStats> OnStatsUpdated;
```

## ResourceManager 规格

### 资源类型

```
┌─────────────────────────────────────────────────────────┐
│                   ResourceManager                       │
├─────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────┐   │
│  │  Coins (金币)                                   │   │
│  │  - 用途: 购买、升级                             │   │
│  │  - 获取: 击杀敌人、掉落、任务                   │   │
│  └─────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────┐   │
│  │  Experience (经验值)                            │   │
│  │  - 用途: 玩家升级                               │   │
│  │  - 获取: 击杀敌人、完成任务                     │   │
│  └─────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────┐   │
│  │  Gems (宝石)                                    │   │
│  │  - 用途: 高级物品、复活                         │   │
│  │  - 获取: 特殊事件、成就、付费                   │   │
│  └─────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────┐   │
│  │  Keys (钥匙)                                    │   │
│  │  - 用途: 开启宝箱、门                           │   │
│  │  - 获取: 掉落、任务                             │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### 资源数据接口

```csharp
/// <summary>
/// 资源数据
/// </summary>
[System.Serializable]
public class ResourceData
{
    public int coins;
    public int totalCoinsEarned;      // 总获得金币
    public int experience;
    public int playerLevel;
    public int gems;
    public int keys;
    public int skillPoints;             // 技能点
}

/// <summary>
/// 资源事件参数
/// </summary>
public class ResourceEventArgs : EventArgs
{
    public ResourceType ResourceType { get; set; }
    public int OldValue { get; set; }
    public int NewValue { get; set; }
    public int ChangeAmount => NewValue - OldValue;
    public string Source { get; set; }  // 变化来源
}
```

### 资源管理方法

```csharp
/// <summary>
/// 添加金币
/// </summary>
public void AddCoins(int amount, string source = "")

/// <summary>
/// 消耗金币
/// </summary>
public bool SpendCoins(int amount, string source = "")

/// <summary>
/// 添加经验值
/// </summary>
public void AddExperience(int amount, string source = "")

/// <summary>
/// 检查资源是否足够
/// </summary>
public bool HasEnoughResources(ResourceCost cost)

/// <summary>
/// 消费资源
/// </summary>
public bool ConsumeResources(ResourceCost cost, string source = "")
```

## SaveSystem 规格

### 存档类型

| 类型 | 文件路径 | 说明 |
|------|----------|------|
| `GameData` | `savegame.dat` | 游戏进度数据 |
| `ResourceData` | `resources.dat` | 资源数据 |
| `Settings` | `settings.json` | 游戏设置 |
| `PlayerStats` | `playerstats.dat` | 玩家统计数据 |

### 存档数据结构

```csharp
/// <summary>
/// 存档数据 (主容器)
/// </summary>
[System.Serializable]
public class SaveData
{
    public string version;              // 存档版本
    public string saveDate;             // 保存日期
    public string playTime;             // 游戏时长

    public GameProgressData gameProgress;
    public ResourceData resources;
    public PlayerStatsData playerStats;
    public LevelProgressData levelProgress;
}

/// <summary>
/// 游戏进度数据
/// </summary>
[System.Serializable]
public class GameProgressData
{
    public string currentLevel;
    public int currentWave;
    public int difficulty;              // 难度等级
    public bool isNewGame;
    public string checkpointId;
    public Dictionary<string, bool> unlockedLevels;
}

/// <summary>
/// 设置数据
/// </summary>
[System.Serializable]
public class SettingsData
{
    // 显示设置
    public int resolutionIndex;
    public int qualityLevel;
    public bool fullscreen;
    public float brightness;

    // 音频设置
    public float masterVolume;
    public float bgmVolume;
    public float sfxVolume;

    // 游戏设置
    public float mouseSensitivity;
    public bool invertY;
    public int difficulty;
    public string language;
}
```

### 存档管理接口

```csharp
/// <summary>
/// 保存游戏
/// </summary>
public static void SaveGame(SaveData data, string slot = "0")

/// <summary>
/// 加载游戏
/// </summary>
public static SaveData LoadGame(string slot = "0")

/// <summary>
/// 检查存档是否存在
/// </summary>
public static bool HasSaveFile(string slot = "0")

/// <summary>
/// 删除存档
/// </summary>
public static void DeleteSave(string slot = "0")

/// <summary>
/// 获取所有存档槽信息
/// </summary>
public static SaveSlotInfo[] GetAllSaveSlots()

/// <summary>
/// 保存设置
/// </summary>
public static void SaveSettings(SettingsData settings)

/// <summary>
/// 加载设置
/// </summary>
public static SettingsData LoadSettings()
```

## 依赖关系

```
GameSystem
├── SaveSystem
│   ├── File I/O (Unity/Mono)
│   └── JsonUtility (Serialization)
├── ResourceManager
│   ├── SaveSystem (持久化)
│   └── GameManager (游戏状态)
├── GameManager
│   ├── EnemySystem (敌人生成)
│   ├── ResourceManager (资源)
│   ├── SaveSystem (存档)
│   └── AudioManager (音乐)
└── DropManager
    ├── ResourceManager (添加资源)
    └── Utils/ObjectPool (对象池)
```

## 测试要点

1. **功能测试**: 状态机转换、资源计算、存档读写
2. **性能测试**: 存档保存/加载速度、大数值资源处理
3. **边界测试**: 满级经验、最大金币、多存档槽
4. **兼容性测试**: 版本升级后的存档兼容性
5. **异常测试**: 存档损坏恢复、磁盘满处理
