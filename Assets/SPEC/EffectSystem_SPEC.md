# 特效系统 (Effect System) 规格说明书

## 模块概述

特效系统负责管理游戏中的所有视觉特效和粒子效果，包括爆炸、火花、升级特效、环境效果等。系统采用对象池管理特效实例，优化性能。

## 架构设计

### 核心组件

```
┌─────────────────────────────────────────────────────────────┐
│                    EffectManager                             │
│                   (单例管理器)                               │
├─────────────────────────────────────────────────────────────┤
│  - 特效注册表 (名称 → 预制体)                                │
│  - 对象池管理 (按类型分类池化)                               │
│  - 自动回收机制 (基于生命周期)                               │
│  - 特效实例追踪 (批量管理)                                   │
└─────────────────────────────────────────────────────────────┘
```

### 类图

```
┌──────────────────────┐
│   EffectManager      │
│   (Singleton)        │
├──────────────────────┤
│ - effectPrefabs      │◀───┐
│ - effectPools        │    │
│ - activeEffects      │    │
├──────────────────────┤    │
│ + RegisterEffect()   │    │
│ + PlayEffect()       │────┘
│ + StopEffect()       │
└──────────────────────┘

┌──────────────────────┐
│   EffectPool         │
├──────────────────────┤
│ - pool               │
│ - prefab             │
├──────────────────────┤
│ + Get()              │
│ + Release()          │
└──────────────────────┘
```

## 核心功能规格

### 1. 特效注册

```csharp
/// <summary>
/// 注册特效预制体
/// </summary>
/// <param name="name">特效名称</param>
/// <param name="prefab">特效预制体</param>
public void RegisterEffect(string name, GameObject prefab)
{
    if (!effectPrefabs.ContainsKey(name))
    {
        effectPrefabs[name] = prefab;
        // 预创建对象池
        CreateEffectPool(name, prefab);
    }
}

/// <summary>
/// 批量注册特效
/// </summary>
public void RegisterEffects(Dictionary<string, GameObject> effects)
{
    foreach (var kvp in effects)
    {
        RegisterEffect(kvp.Key, kvp.Value);
    }
}
```

### 2. 特效播放

```csharp
/// <summary>
/// 播放特效
/// </summary>
/// <param name="name">特效名称</param>
/// <param name="position">播放位置</param>
/// <param name="parent">父物体(可选)</param>
/// <returns>特效实例</returns>
public GameObject PlayEffect(string name, Vector3 position, Transform parent = null)
{
    if (!effectPrefabs.TryGetValue(name, out GameObject prefab) || prefab == null)
    {
        Debug.LogWarning($"Effect '{name}' not found!");
        return null;
    }

    // 从对象池获取
    GameObject effect = GetEffectFromPool(name, prefab);
    if (effect == null)
    {
        // 池已满，直接实例化
        effect = Instantiate(prefab);
    }

    // 设置位置和父物体
    effect.transform.position = position;
    if (parent != null)
    {
        effect.transform.SetParent(parent, true);
    }

    // 激活特效
    effect.SetActive(true);

    // 追踪活动特效
    TrackActiveEffect(name, effect);

    // 自动回收
    StartCoroutine(ReturnEffectToPool(name, effect));

    return effect;
}

/// <summary>
/// 在父物体上播放特效
/// </summary>
public GameObject PlayEffect(string name, Transform parent)
{
    if (parent == null) return null;
    return PlayEffect(name, parent.position, parent);
}
```

### 3. 对象池管理

```csharp
/// <summary>
/// 创建特效池
/// </summary>
private void CreateEffectPool(string name, GameObject prefab, int initialSize = 5)
{
    Queue<GameObject> pool = new Queue<GameObject>();

    for (int i = 0; i < initialSize; i++)
    {
        GameObject effect = Instantiate(prefab);
        effect.SetActive(false);
        pool.Enqueue(effect);
    }

    effectPools[name] = new EffectPool
    {
        pool = pool,
        prefab = prefab
    };
}

/// <summary>
/// 从对象池获取特效
/// </summary>
private GameObject GetEffectFromPool(string name, GameObject prefab)
{
    if (effectPools.TryGetValue(name, out EffectPool pool))
    {
        if (pool.pool.Count > 0)
        {
            return pool.pool.Dequeue();
        }
    }

    return null;
}

/// <summary>
/// 返回特效到对象池
/// </summary>
private IEnumerator ReturnEffectToPool(string name, GameObject effect)
{
    // 等待特效播放完毕
    float lifetime = 2f; // 默认2秒

    // 如果有粒子系统，使用其持续时间
    var particleSystem = effect.GetComponent<ParticleSystem>();
    if (particleSystem != null)
    {
        lifetime = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
    }

    yield return new WaitForSeconds(lifetime);

    // 回收
    if (effect != null)
    {
        effect.SetActive(false);
        effect.transform.SetParent(null);

        if (effectPools.TryGetValue(name, out EffectPool pool))
        {
            pool.pool.Enqueue(effect);
        }
        else
        {
            Destroy(effect);
        }
    }
}
```

## 预定义特效列表

| 特效名称 | 类型 | 用途 |
|----------|------|------|
| `EnemyDeath` | 爆炸 | 敌人死亡 |
| `EnemyHit` | 粒子 | 敌人受击 |
| `PlayerDamage` | 屏幕效果 | 玩家受伤 |
| `TurretFire` | 闪光+烟雾 | 炮塔开火 |
| `BulletHit` | 火花 | 子弹命中 |
| `Explosion` | 爆炸 | 大范围伤害 |
| `UpgradeSuccess` | 光效 | 升级成功 |
| `UpgradeFail` | 粒子 | 升级失败 |
| `CoinPickup` | 闪光 | 拾取金币 |
| `LevelUp` | 光柱 | 玩家升级 |

## 依赖关系

```
EffectSystem
├── Unity ParticleSystem (粒子系统)
└── Utils/ObjectPool (对象池)
```

## 测试要点

1. **功能测试**: 特效正确播放、位置正确、自动回收
2. **性能测试**: 大量特效并发、内存占用、GC频率
3. **边界测试**: 预制体缺失、位置无效、父物体销毁
