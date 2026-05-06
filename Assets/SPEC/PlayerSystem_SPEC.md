# 玩家系统 (Player System) 规格说明书

## 模块概述

玩家系统负责管理玩家的核心属性，包括生命值系统、伤害处理、治疗机制、死亡与复活等功能。该系统独立于移动系统，专注于玩家生存状态的管理。

## 架构设计

### 核心组件

```
┌─────────────────────────────────────────────────────────────┐
│                     PlayerHealth                            │
│              (玩家生命管理器 - MonoBehaviour)                 │
├─────────────────────────────────────────────────────────────┤
│  - 生命值管理 (当前/最大/百分比)                            │
│  - 伤害处理 (减伤计算、暴击处理)                            │
│  - 治疗机制 (立即治疗、持续回复)                            │
│  - 无敌系统 (受伤后无敌帧)                                  │
│  - 死亡与复活 (死亡判定、复活点)                            │
│  - 升级系统 (最大生命成长)                                  │
└─────────────────────────────────────────────────────────────┘
```

### 类图

```
┌─────────────────────────────────────────────────────────┐
│                    PlayerHealth                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │ Properties:                                     │   │
│  │   - CurrentHealth (float, 0-Max)                │   │
│  │   - MaxHealth (float, base + level growth)      │   │
│  │   - HealthPercent (float, 0-1)                  │   │
│  │   - IsDead (bool)                               │   │
│  │   - IsInvincible (bool)                        │   │
│  └─────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────┐   │
│  │ Events:                                         │   │
│  │   - OnHealthChanged (curr, max)                 │   │
│  │   - OnDamaged (damage, attacker)                │   │
│  │   - OnHealed (amount)                           │   │
│  │   - OnDeath ()                                  │   │
│  │   - OnRespawn ()                                │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## 核心功能规格

### 1. 基础属性

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `maxHealth` | float | 100f | 最大生命值(可成长) |
| `initialHealth` | float | 100f | 初始生命值 |
| `invincibilityTime` | float | 1f | 受伤后无敌时间(秒) |
| `healthRegenPerSecond` | float | 0f | 每秒自然回血 |
| `regenDelay` | float | 5f | 受伤后多久开始回血 |

### 2. 伤害系统

```csharp
/// <summary>
/// 受到伤害
/// </summary>
/// <param name="damage">伤害值</param>
/// <param name="attacker">攻击者(可为null)</param>
/// <param name="isCritical">是否暴击</param>
public void TakeDamage(float damage, Transform attacker = null, bool isCritical = false)
{
    // 死亡或无敌时忽略伤害
    if (isDead || isInvincible)
        return;

    // 计算实际伤害(可加入护甲减伤等)
    float finalDamage = CalculateFinalDamage(damage, attacker);

    // 扣除生命
    currentHealth -= finalDamage;
    currentHealth = Mathf.Max(0, currentHealth);

    // 触发受伤事件
    OnHealthChanged?.Invoke(currentHealth, maxHealth);
    OnDamaged?.Invoke(finalDamage, attacker, isCritical);

    // 播放受伤效果
    PlayDamageEffects();

    // 启动无敌帧
    StartInvincibility();

    // 重置回血计时
    lastDamageTime = Time.time;

    // 检查死亡
    if (currentHealth <= 0)
    {
        Die();
    }
}

/// <summary>
/// 计算最终伤害
/// </summary>
private float CalculateFinalDamage(float baseDamage, Transform attacker)
{
    // 基础伤害
    float damage = baseDamage;

    // TODO: 护甲减伤
    // damage *= (1 - damageReduction);

    // TODO: 伤害类型加成/减免
    // if (attacker != null)
    // {
    //     var damageType = attacker.GetComponent<DamageDealer>()?.DamageType;
    //     damage *= GetResistanceMultiplier(damageType);
    // }

    return Mathf.Max(1, damage); // 最小伤害为1
}
```

### 3. 治疗系统

```csharp
/// <summary>
/// 恢复生命值
/// </summary>
/// <param name="amount">治疗量</param>
/// <param name="showEffect">是否显示特效</param>
public void Heal(float amount, bool showEffect = true)
{
    if (isDead || amount <= 0)
        return;

    float oldHealth = currentHealth;
    currentHealth += amount;
    currentHealth = Mathf.Min(currentHealth, maxHealth);

    float actualHeal = currentHealth - oldHealth;

    if (actualHeal > 0)
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnHealed?.Invoke(actualHeal);

        if (showEffect)
        {
            PlayHealEffect();
        }
    }
}

/// <summary>
/// 持续恢复生命值
/// </summary>
/// <param name="totalAmount">总治疗量</param>
/// <param name="duration">持续时间</param>
public void HealOverTime(float totalAmount, float duration)
{
    StartCoroutine(HealOverTimeCoroutine(totalAmount, duration));
}

private IEnumerator HealOverTimeCoroutine(float totalAmount, float duration)
{
    float healPerSecond = totalAmount / duration;
    float elapsed = 0f;

    while (elapsed < duration && !isDead)
    {
        yield return new WaitForSeconds(1f);
        Heal(healPerSecond, false);
        elapsed += 1f;
    }
}

/// <summary>
/// 立即恢复全部生命
/// </summary>
public void FullHeal()
{
    Heal(maxHealth, true);
}
```

### 4. 死亡与复活

```csharp
/// <summary>
/// 死亡处理
/// </summary>
private void Die()
{
    if (isDead)
        return;

    isDead = true;
    currentHealth = 0;

    // 触发死亡事件
    OnDeath?.Invoke();

    // 播放死亡效果
    PlayDeathEffects();

    // 通知游戏管理器
    GameManager.Instance?.OnPlayerDied();
}

/// <summary>
/// 复活玩家
/// </summary>
/// <param name="respawnPosition">复活位置</param>
/// <param name="healthPercent">复活时的生命百分比(0-1)</param>
public void Respawn(Vector3 respawnPosition, float healthPercent = 0.5f)
{
    if (!isDead)
        return;

    // 重置状态
    isDead = false;
    currentHealth = maxHealth * healthPercent;

    // 移动到复活点
    transform.position = respawnPosition;

    // 触发复活事件
    OnRespawn?.Invoke();

    // 播放复活效果
    PlayRespawnEffects();

    Debug.Log($"玩家已复活，当前生命: {currentHealth}/{maxHealth}");
}

/// <summary>
/// 检查点复活（保留部分状态）
/// </summary>
public void RespawnAtCheckpoint()
{
    // 获取检查点位置
    Vector3 checkpointPos = GetLastCheckpointPosition();

    // 复活，但保留经验值、金币等
    Respawn(checkpointPos, 1.0f); // 满血复活

    // 可选：扣除部分金币作为惩罚
    // ResourceManager.Instance?.SpendCoins(10);
}
```

### 5. 升级成长

```csharp
/// <summary>
/// 增加最大生命值
/// </summary>
/// <param name="amount">增加量</param>
/// <param name="heal">是否同时恢复生命</param>
public void IncreaseMaxHealth(float amount, bool heal = true)
{
    float oldMax = maxHealth;
    maxHealth += amount;

    if (heal)
    {
        // 恢复增加的生命值
        Heal(amount);
    }

    // 触发最大生命值改变事件
    OnMaxHealthChanged?.Invoke(oldMax, maxHealth);
    OnHealthChanged?.Invoke(currentHealth, maxHealth);

    Debug.Log($"最大生命值提升: {oldMax} → {maxHealth}");
}

/// <summary>
/// 玩家升级时调用
/// </summary>
/// <param name="newLevel">新等级</param>
public void OnPlayerLevelUp(int newLevel)
{
    // 每级增加10点最大生命
    IncreaseMaxHealth(10f, true);

    // 可选：恢复全部生命
    // FullHeal();
}
```

## 接口定义

### 公共属性

```csharp
// 生命相关
public float CurrentHealth { get; }
public float MaxHealth { get; }
public float HealthPercent { get; } // 0-1
public bool IsDead { get; }
public bool IsInvincible { get; }

// 状态
public bool IsHealthRegenerating { get; }
public float TimeSinceLastDamage { get; }
```

### 事件

```csharp
/// <summary>
/// 生命值改变事件 (当前值, 最大值)
/// </summary>
public event Action<float, float> OnHealthChanged;

/// <summary>
/// 最大生命值改变事件 (旧值, 新值)
/// </summary>
public event Action<float, float> OnMaxHealthChanged;

/// <summary>
/// 受到伤害事件 (伤害值, 攻击者, 是否暴击)
/// </summary>
public event Action<float, Transform, bool> OnDamaged;

/// <summary>
/// 受到治疗事件 (治疗量)
/// </summary>
public event Action<float> OnHealed;

/// <summary>
/// 死亡事件
/// </summary>
public event Action OnDeath;

/// <summary>
/// 复活事件
/// </summary>
public event Action OnRespawn;

/// <summary>
/// 无敌状态改变事件 (是否无敌)
/// </summary>
public event Action<bool> OnInvincibilityChanged;
```

## 性能优化

### 1. 减少GC分配

```csharp
// 使用预分配的数组
private static Collider[] hitColliders = new Collider[10];

// 缓存字符串，避免频繁拼接
private const string ANIM_HIT = "Hit";
private const string ANIM_DEATH = "Death";
```

### 2. 优化更新逻辑

```csharp
// 将回血逻辑从Update移到协程
private IEnumerator HealthRegenCoroutine()
{
    while (true)
    {
        yield return new WaitForSeconds(1f); // 每秒检查一次

        if (!isDead && currentHealth < maxHealth &&
            Time.time - lastDamageTime > regenDelay)
        {
            Heal(healthRegenPerSecond, false);
        }
    }
}
```

## 配置示例

```json
{
  "playerHealth": {
    "baseMaxHealth": 100,
    "healthPerLevel": 10,
    "invincibilityTime": 1.0,
    "healthRegenPerSecond": 0,
    "regenDelay": 5.0,
    "damageFlashColor": {"r": 1.0, "g": 0.0, "b": 0.0, "a": 0.3},
    "flashDuration": 0.2,
    "respawn": {
      "respawnDelay": 3.0,
      "respawnHealthPercent": 0.5,
      "penaltyCoinsPercent": 0.1
    }
  }
}
```

## 依赖关系

```
PlayerSystem
├── Camera/CameraShake (受伤震动)
├── Effects/EffectManager (受伤特效)
├── Audio/AudioManager (受伤音效)
├── Game/GameManager (死亡通知)
└── Game/ResourceManager (复活惩罚)
```

## 测试要点

1. **功能测试**: 伤害计算、治疗效果、死亡判定、复活逻辑
2. **性能测试**: 高频伤害处理、持续回血效率
3. **边界测试**: 满血治疗、0伤害、即死伤害、多次快速死亡
4. **集成测试**: 与UI血量条同步、与升级系统联动
