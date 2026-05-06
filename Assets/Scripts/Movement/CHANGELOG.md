# Movement System 重构更新日志

## [3.0.0] - 2026-03-22

### 重大变更

- **架构重构**: 完全重写的 Movement 系统，采用更清晰的分层架构
- **职责分离**: 球面环境、移动逻辑、输入处理完全解耦
- **策略模式**: 使用策略模式支持平面和球面两种移动模式

### 新增组件

#### 环境组件
- `SphereSurface` - 定义球面环境的组件

#### 移动组件
- `SurfaceMovement` - 主移动控制器，支持平面/球面模式切换
- `PlaneMovementStrategy` - 平面移动策略实现
- `SphericalMovementStrategy` - 球面移动策略实现

#### 输入组件
- `MovementInput` - 统一的输入处理组件
- `SurfaceMovementInput` - 移动输入处理器

#### 摄像机组件
- `ThirdPersonCamera` - 全新的第三人称摄像机控制器

#### 数据配置
- `MovementConfig` - 集中式配置管理 ScriptableObject

#### 接口定义
- `IMovementStrategy` - 移动策略接口
- `ISurface` - 表面接口

### 改进

1. **可配置性**: 所有参数通过 MovementConfig 集中管理
2. **可扩展性**: 通过实现接口轻松添加新的移动策略
3. **可测试性**: 依赖注入支持，便于单元测试
4. **代码质量**: 遵循 SOLID 原则，单一职责清晰

### 删除

- `SphereMovement.cs` - 旧的主控制器
- `SphereCameraController.cs` - 旧的摄像机控制器
- `SphericalCoordinates.cs` - 工具类功能合并到新组件
- `Editor/SphereMovementEditor.cs` - 旧编辑器工具
- `Editor/SphereMovementGizmos.cs` - 旧Gizmos工具

### 迁移指南

#### 旧代码:
```csharp
var movement = gameObject.AddComponent<SphereMovement>();
movement.sphereCenter = Vector3.zero;
movement.sphereRadius = 10f;
movement.moveSpeed = 30f;
```

#### 新代码:
```csharp
// 1. 创建球面环境（球面模式需要）
var surface = sphereGameObject.AddComponent<SphereSurface>();
surface.Center = Vector3.zero;
surface.Radius = 10f;

// 2. 创建配置
var config = ScriptableObject.CreateInstance<MovementConfig>();
config.sphericalMoveSpeed = 30f;

// 3. 添加移动组件
var movement = gameObject.AddComponent<SurfaceMovement>();
movement.SetMovementMode(MovementMode.Spherical, surface);
```

### 依赖关系

```
SurfaceMovement
├── MovementConfig (ScriptableObject)
├── MovementInput (Component)
├── SphereSurface (Component, 球面模式)
└── IMovementStrategy (Interface)
    ├── PlaneMovementStrategy
    └── SphericalMovementStrategy

ThirdPersonCamera
└── Transform (Target)
```

### 性能考虑

1. **零GC分配**: 核心移动逻辑无堆分配
2. **缓存优化**: Transform和配置数据已缓存
3. **可选功能**: 碰撞检测等可选功能仅在启用时消耗性能

### 已知限制

1. 球面模式不支持非常大的球体（浮点精度限制）
2. 碰撞检测使用SphereCast，可能对性能敏感
3. 第三人称摄像机在极近距离可能有视觉问题
