# 敌人系统 (Enemy System) 规格说明书

## 模块概述

敌人系统是游戏中负责所有敌人生成、AI行为、战斗和生命周期的核心系统。采用状态机模式管理敌人行为，支持多种敌人类型和扩展机制。

## 架构设计

### 核心组件

```
┌─────────────────────────────────────────────────────────────┐
│                    EnemyBase (Abstract)                       │
├─────────────────────────────────────────────────────────────┤
│  - 状态机管理 (Idle/Patrol/Chase/Attack/Dead)                │
│  - 生命系统 (Health/Damage/Death)                            │
│  - 目标检测 (Range/Layer/Tag)                                │
│  - 掉落系统 (Coin/Exp/Items)                                 │
└─────────────────────────────────────────────────────────────┘
```

### 类图

```
                    ┌──────────────────┐
                    │    EnemyBase     │
                    │   (Abstract)     │
                    └────────┬─────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
┌───────▼────────┐  ┌────────▼────────┐  ┌─────────▼────────┐
│  EnemyNormal   │  │   EnemyFast    │  │    EnemyTank     │
│  (Balanced)    │  │   (Speed)      │  │   (Defense)      │
└────────────────┘  └────────────────┘  └──────────────────┘

┌─────────────────────────────────────────────────────────┐
│                     StateMachine                        │
├─────────────────────────────────────────────────────────┤
│  - EnterState/ExitState/UpdateState                     │
│  - 状态转换条件检查                                       │
│  - 状态行为执行                                          │
└─────────────────────────────────────────────────────────┘
```

## 核心功能规格

### 1. 状态机系统

| 状态 | 枚举值 | 行为描述 |
|------|--------|----------|
| `Idle` | 0 | 待机：原地静止，检测玩家 |
| `Patrol` | 1 | 巡逻：在区域内随机移动 |
| `Chase` | 2 | 追击：向玩家方向移动 |
| `Attack` | 3 | 攻击：在范围内攻击玩家 |
| `Dead` | 4 | 死亡：播放死亡动画并销毁 |

**状态转换图**:

```
                    ┌─────────────┐
         ┌─────────▶│    Idle     │◀──────────┐
         │          └──────┬──────┘           │
         │                 │                 │
         │                 ▼                 │
         │          ┌─────────────┐        │
         │    ┌─────│   Patrol    │────┐   │
         │    │     └─────────────┘    │   │
         │    │                        │   │
         │    └────────────────────────┘   │
         │                                  │
         │          ┌─────────────┐        │
         └─────────│    Chase    │────────┘
    (丢失目标)      └──────┬──────┘      (发现目标)
                          │
                          ▼
                   ┌─────────────┐
                   │    Attack   │
                   └─────────────┘
                          │
                          ▼
                   ┌─────────────┐
                   │     Dead    │
                   └─────────────┘
```

### 2. 敌人类型配置

#### 普通敌人 (EnemyNormal)

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `maxHealth` | 100f | 最大生命值 |
| `moveSpeed` | 3f | 移动速度 |
| `attackDamage` | 10f | 攻击力 |
| `attackCooldown` | 1f | 攻击冷却 |
| `patrolRadius` | 5f | 巡逻半径 |

#### 快速敌人 (EnemyFast)

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `maxHealth` | 50f | 最大生命值 |
| `moveSpeed` | 6f | 移动速度 |
| `sprintMultiplier` | 1.5f | 冲刺倍率 |
| `sprintDuration` | 2f | 冲刺时长 |
| `dodgeChance` | 0.3f | 闪避概率 |

#### 坦克敌人 (EnemyTank)

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `maxHealth` | 300f | 最大生命值 |
| `moveSpeed` | 1.5f | 移动速度 |
| `shieldValue` | 50f | 护盾值 |
| `shieldRegenRate` | 5f | 护盾恢复速度 |
| `chargeDistance` | 10f | 冲锋距离 |

#### 飞行敌人 (EnemyFlying)

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `maxHealth` | 80f | 最大生命值 |
| `moveSpeed` | 4f | 移动速度 |
| `flyHeight` | 5f | 飞行高度 |
| `diveAttackRange` | 8f | 俯冲范围 |
| `circleRadius` | 3f | 盘旋半径 |

### 3. 生命系统

```csharp
/// <summary>
/// 受到伤害
/// </summary>
public virtual void TakeDamage(float damage)
{
    if (isDead) return;

    currentHealth -= damage;

    // 播放受击特效
    PlayHitEffect();

    // 播放受击动画
    PlayAnimation("Hit");

    // 触发受伤事件
    OnDamageTaken(damage);

    if (currentHealth <= 0)
    {
        Die();
    }
}

/// <summary>
/// 死亡处理
/// </summary>
protected virtual void Die()
{
    if (isDead) return;

    isDead = true;
    currentState = EnemyState.Dead;

    // 禁用碰撞器
    if (enemyCollider != null)
        enemyCollider.enabled = false;

    // 播放死亡特效
    PlayDeathEffect();

    // 播放死亡动画
    PlayAnimation("Death");

    // 掉落物品
    DropItems();

    // 触发死亡事件
    OnDeath();

    // 延迟销毁
    StartCoroutine(DestroyAfterDelay(3f));
}
```

### 4. 掉落系统

```csharp
/// <summary>
/// 掉落物品
/// </summary>
protected virtual void DropItems()
{
    // 金币
    int coins = Mathf.RoundToInt(coinDropAmount * Random.Range(0.8f, 1.2f));
    DropManager.Instance?.SpawnCoin(transform.position, coins);

    // 经验值
    int exp = experienceValue;
    DropManager.Instance?.SpawnExperience(transform.position, exp);

    // 随机掉落物品
    if (Random.value <= dropChance)
    {
        DropManager.Instance?.SpawnRandomItem(transform.position);
    }
}
```

## 接口定义

### 公共方法

```csharp
/// <summary>
/// 设置目标
/// </summary>
public virtual void SetTarget(Transform playerTarget)

/// <summary>
/// 受到伤害
/// </summary>
public virtual void TakeDamage(float damage)

/// <summary>
/// 恢复生命
/// </summary>
public virtual void Heal(float amount)

/// <summary>
/// 眩晕控制
/// </summary>
public virtual void Stun(float duration)

/// <summary>
/// 击退效果
/// </summary>
public virtual void Knockback(Vector3 direction, float force)
```

### 事件

```csharp
/// <summary>
/// 敌人死亡事件
/// </summary>
public static event Action<EnemyBase> OnEnemyDied;

/// <summary>
/// 敌人受伤事件
/// </summary>
public event Action<float> OnEnemyDamaged;

/// <summary>
/// 状态改变事件
/// </summary>
public event Action<EnemyState> OnStateChanged;
```

## 性能优化

### 1. 目标检测优化

```csharp
// 使用非分配式检测
private Collider[] hitColliders = new Collider[20];

private void FindTargetNonAlloc()
{
    int numColliders = Physics.OverlapSphereNonAlloc(
        transform.position,
        detectionRange,
        hitColliders,
        targetLayer
    );

    float closestDistance = float.MaxValue;
    Transform closestTarget = null;

    for (int i = 0; i < numColliders; i++)
    {
        float distance = Vector3.Distance(transform.position, hitColliders[i].transform.position);
        if (distance < closestDistance)
        {
            closestDistance = distance;
            closestTarget = hitColliders[i].transform;
        }
    }

    target = closestTarget;
}
```

### 2. LOD系统

```csharp
public class EnemyLOD : MonoBehaviour
{
    [Header("LOD设置")]
    public float highDetailDistance = 20f;
    public float mediumDetailDistance = 50f;
    public float cullDistance = 100f;

    [Header("组件引用")]
    public Animator animator;
    public ParticleSystem effects;
    public MonoBehaviour[] aiScripts;

    private Transform player;
    private float updateInterval = 0.5f;
    private float lastUpdate;

    void Update()
    {
        if (Time.time - lastUpdate < updateInterval) return;
        lastUpdate = Time.time;

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        UpdateLOD(distance);
    }

    private void UpdateLOD(float distance)
    {
        if (distance > cullDistance)
        {
            // 完全隐藏
            SetActive(false);
        }
        else if (distance > mediumDetailDistance)
        {
            // 低细节：禁用AI和特效
            SetAIEnabled(false);
            SetEffectsEnabled(false);
        }
        else if (distance > highDetailDistance)
        {
            // 中等细节：启用AI，减少特效
            SetAIEnabled(true);
            SetEffectsEnabled(false);
        }
        else
        {
            // 高细节：完全启用
            SetActive(true);
            SetAIEnabled(true);
            SetEffectsEnabled(true);
        }
    }

    private void SetActive(bool active)
    {
        if (gameObject.activeSelf != active)
        {
            gameObject.SetActive(active);
        }
    }

    private void SetAIEnabled(bool enabled)
    {
        foreach (var script in aiScripts)
        {
            script.enabled = enabled;
        }
    }

    private void SetEffectsEnabled(bool enabled)
    {
        if (effects != null)
        {
            if (enabled && !effects.isPlaying)
                effects.Play();
            else if (!enabled && effects.isPlaying)
                effects.Stop();
        }
    }
}
```

## 配置示例

```json
{
  "enemies": {
    "normal": {
      "maxHealth": 100,
      "moveSpeed": 3,
      "attackDamage": 10,
      "attackCooldown": 1,
      "detectionRange": 10,
      "attackRange": 1.5,
      "coinDrop": 10,
      "experience": 20
    },
    "fast": {
      "maxHealth": 50,
      "moveSpeed": 6,
      "sprintMultiplier": 1.5,
      "dodgeChance": 0.3,
      "attackDamage": 5,
      "coinDrop": 15,
      "experience": 25
    },
    "tank": {
      "maxHealth": 300,
      "moveSpeed": 1.5,
      "shieldValue": 50,
      "shieldRegenRate": 5,
      "chargeDamage": 30,
      "attackDamage": 20,
      "coinDrop": 30,
      "experience": 50
    },
    "flying": {
      "maxHealth": 80,
      "moveSpeed": 4,
      "flyHeight": 5,
      "diveAttackDamage": 15,
      "attackDamage": 8,
      "coinDrop": 20,
      "experience": 30
    }
  }
}
```

## 依赖关系

```
EnemySystem
├── Effects/EffectManager (特效)
├── Game/GameManager (游戏状态)
├── Game/DropManager (掉落)
├── Player/PlayerHealth (伤害)
├── Utils/ObjectPool (对象池)
└── Audio/AudioManager (音效)
```

## 测试要点

1. **功能测试**: 状态转换正确性、AI决策逻辑、攻击判定
2. **性能测试**: 大量敌人时的帧率、检测频率优化
3. **边界测试**: 玩家死亡时敌人行为、LOD切换平滑度
4. **平衡测试**: 各类型敌人难度曲线、掉落合理性
