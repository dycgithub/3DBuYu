# 3DBuYu 游戏系统规格文档索引

本文档索引列出了游戏中所有核心系统的规格说明书。

## 规格文档列表

### 核心战斗系统

| 文档 | 说明 | 关键类 |
|------|------|--------|
| [TurretSystem_SPEC.md](./TurretSystem_SPEC.md) | 炮塔系统规格 | Turret, TurretLevelData |
| [BulletSystem_SPEC.md](./BulletSystem_SPEC.md) | 子弹系统规格 | Bullet, BulletConfig |
| [EnemySystem_SPEC.md](./EnemySystem_SPEC.md) | 敌人系统规格 | EnemyBase, StateMachine |

### 移动与相机

| 文档 | 说明 | 关键类 |
|------|------|--------|
| [MovementSystem_SPEC.md](./MovementSystem_SPEC.md) | 移动系统规格 | SphereMovement, SphericalCoordinates |
| [CameraSystem_SPEC.md](./CameraSystem_SPEC.md) | 相机系统规格 | CameraFollow, SphereCameraController, CameraShake |

### 玩家与游戏系统

| 文档 | 说明 | 关键类 |
|------|------|--------|
| [PlayerSystem_SPEC.md](./PlayerSystem_SPEC.md) | 玩家系统规格 | PlayerHealth |
| [GameSystem_SPEC.md](./GameSystem_SPEC.md) | 游戏系统规格 | GameManager, ResourceManager, SaveSystem, DropManager |

### 特效与音频

| 文档 | 说明 | 关键类 |
|------|------|--------|
| [EffectSystem_SPEC.md](./EffectSystem_SPEC.md) | 特效系统规格 | EffectManager |
| [AudioSystem_SPEC.md](./AudioSystem_SPEC.md) | 音频系统规格 | AudioManager |

### 工具系统

| 文档 | 说明 | 关键类 |
|------|------|--------|
| [UtilsSystem_SPEC.md](./UtilsSystem_SPEC.md) | 工具系统规格 | ObjectPool, SphericalCoordinates |

## 快速参考

### 系统依赖关系图

```
GameManager
├── ResourceManager
├── SaveSystem
├── AudioManager
├── EffectManager
└── EnemySpawnManager
    └── EnemyBase
        ├── EnemyNormal
        ├── EnemyFast
        ├── EnemyTank
        └── EnemyFlying

PlayerHealth
├── CameraShake
└── EffectManager

Turret
├── Bullet (ObjectPool)
└── EffectManager

SphereMovement
├── SphericalCoordinates
└── SphereCameraController

DropManager
├── ResourceManager
└── EffectManager
```

### 常用配置路径

| 配置项 | 路径 |
|--------|------|
| 存档文件 | `Application.persistentDataPath/savegame.dat` |
| 设置文件 | `Application.persistentDataPath/settings.json` |
| BGM音频 | `Assets/Audio/BGM/` |
| SFX音频 | `Assets/Audio/SFX/` |
| 特效预制体 | `Assets/Prefabs/Effects/` |

### 关键常量

| 常量 | 值 | 说明 |
|------|-----|------|
| `DEFAULT_POOL_CAPACITY` | 10 | 默认对象池容量 |
| `MAX_POOL_SIZE` | 100 | 最大对象池大小 |
| `BGM_FADE_TIME` | 2.0s | BGM淡入时间 |
| `INVINCIBILITY_TIME` | 1.0s | 无敌时间 |
| `SPHERE_RADIUS` | 50.0f | 默认球体半径 |

---

*文档版本: 1.0*
*最后更新: 2026-03-21*
