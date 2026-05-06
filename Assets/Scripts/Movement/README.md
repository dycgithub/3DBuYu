# 球面移动系统 (Refactored)

## 概述

重构后的球面移动系统采用基于接口的模块化设计，提高了代码的可测试性、可扩展性和可维护性。

## 架构

### 接口层 (Interfaces)

- `IInputProvider` - 输入提供接口
- `IMovementInputHandler` - 移动输入处理接口
- `ISphericalPositionCalculator` - 球面位置计算接口
- `ISmoothMovementController` - 平滑移动控制接口
- `IOrientationController` - 朝向控制接口

### 实现层

#### 输入 (Input)
- `UnityInputProvider` - Unity轴输入
- `KeyboardInputProvider` - 键盘按键输入
- `MockInputProvider` - 模拟输入（用于测试）

#### 核心 (Core)
- `SphericalPositionCalculator` - 球面位置计算
- `MovementInputHandler` - 输入处理
- `SmoothMovementController` - 平滑移动
- `OrientationController` - 朝向控制

### 主控制器

- `SphereMovementRefactored` - 重构后的主控制器

## 特性

### 1. 依赖注入支持

```csharp
var movement = GetComponent<SphereMovementRefactored>();
movement.InputHandler = new CustomInputHandler(...);
movement.PositionCalculator = new CustomCalculator(...);
```

### 2. 灵活的输入模式

```csharp
public enum InputMode
{
    UnityAxis,      // Unity轴输入
    Keyboard,     // 键盘输入
    Mock          // 模拟输入（测试用）
}
```

### 3. 完整测试覆盖

- 单元测试：所有核心类
- 集成测试：系统整体功能
- 边界测试：极点、零值等

## 使用方法

### 基础用法

```csharp
// 添加组件
var movement = gameObject.AddComponent<SphereMovementRefactored>();

// 配置参数
movement.sphereCenter = Vector3.zero;
movement.sphereRadius = 10f;
movement.moveSpeed = 45f;
movement.inputMode = InputMode.UnityAxis;
```

### 自定义输入

```csharp
// 创建自定义输入提供器
var customInput = new MockInputProvider();

// 创建使用自定义输入的处理器
var inputHandler = new MovementInputHandler(customInput);

// 注入到主控制器
movement.InputHandler = inputHandler;

// 在游戏中控制输入
customInput.SetHorizontal(1f); // 向右移动
customInput.SetVertical(-1f); // 向下移动
```

### 程序化移动

```csharp
// 直接设置位置
movement.SetPositionOnSphere(45f, 30f); // 经度45度，纬度30度

// 获取当前位置信息
float longitude = movement.CurrentLongitude;
float latitude = movement.CurrentLatitude;
Vector3 worldPos = movement.CurrentPositionOnSphere;
```

## 测试

### 运行测试

1. 打开 Unity Test Runner 窗口：`Window > General > Test Runner`
2. 选择 `Edit Mode` 标签
3. 展开 `SphereMovement.Tests`
4. 点击 `Run All` 或选择特定测试运行

### 测试分类

- `SphericalCoordinatesTests` - 坐标转换工具测试
- `MovementInputHandlerTests` - 输入处理测试
- `SmoothMovementControllerTests` - 平滑移动测试
- `SphericalPositionCalculatorTests` - 位置计算测试
- `OrientationControllerTests` - 朝向控制测试
- `SphericalMovementIntegrationTests` - 集成测试
- `MockInputProviderTests` - 模拟输入测试

### 代码覆盖率

推荐安装 Unity 的 Code Coverage 包来查看测试覆盖率。

## 性能考虑

1. **对象分配**：核心类避免在Update中分配内存
2. **数学运算**：使用快速近似方法（如需要）
3. **缓存**：Transform和其他组件已缓存

## 扩展

### 添加新的输入提供器

```csharp
public class TouchInputProvider : IInputProvider
{
    public float Horizontal { get; private set; }
    public float Vertical { get; private set; }
    public bool HasInput { get; private set; }

    // 实现触摸输入逻辑
}
```

### 添加新的位置计算器

```csharp
public class EllipsoidPositionCalculator : ISphericalPositionCalculator
{
    public Vector3 SphereCenter { get; set; }
    public float SphereRadius { get; set; }

    // 实现椭球面位置计算
    public Vector3 CalculatePosition(Vector2 sphericalCoords)
    {
        // 自定义实现...
        return Vector3.zero;
    }

    // 实现其他方法...
}
```

## 许可证

此代码作为项目的一部分，遵循项目的整体许可证。

## 更新日志

### 2.0.0 (重构版本)
- 基于接口的模块化架构
- 完整的单元测试覆盖
- 依赖注入支持
- 多种输入模式支持
- 改进的代码文档

### 1.0.0 (原始版本)
- 基础球面移动功能
- 平滑移动和朝向控制
