# 特效系统 (Effect System) 规格说明书

## 模块概述

特效系统负责游戏中所有视觉特效，包括战斗反馈、Buff效果、**抽奖轮盘**和通关特效。使用对象池管理以避免 GC 压力。

## GDD 要求的特效

| 事件 | 视觉反馈 |
|------|---------|
| 基础弹命中 | 小型水花粒子 |
| 高级弹命中 | 对应特效（穿透链/爆炸圈/毒液绿） |
| 击杀敌人 | 爆炸粒子 + 分数飘字 |
| 击杀黄金鱼 | 全屏金光 + 轮盘弹出 |
| Buff激活 | 炮台光环变色 |
| 通关 | 球体脉冲扩散 + 烟花粒子 |
| 失败 | 灰色滤镜 + 分数红色闪烁 |
| 轨道/球体扩展 | 脉冲扩散 + 粒子爆发 |
| 购买升级 | 炮台闪光 |

## 架构

```
┌──────────────────────────────────────────────┐
│           EffectManager (Singleton)           │
├──────────────────────────────────────────────┤
│  - 特效注册表 (名称 → 预制体)                  │
│  - 对象池 (按类型分类)                        │
│  - 自动回收 (基于生命周期/ParticleSystem时长)  │
│  - 抽奖轮盘特效 (独立管理)                     │
└──────────────────────────────────────────────┘
```

## 抽奖轮盘特效

击杀黄金鱼触发 `LotteryWheel`:
- 轮盘旋转动画（3秒）
- 指针减速停止
- 奖项揭晓光柱
- 奖品飞入 UI

## 核心接口

```csharp
public GameObject PlayEffect(string name, Vector3 position, Transform parent = null);
public void PlayLotteryWheel(Vector3 position, System.Action<LotteryPrize> onComplete);
public void PlayScorePopup(Vector3 position, int score);
public void PlayBuffActivate(Vector3 position, BuffType type);
```

## 依赖关系

```
EffectSystem
├── Unity ParticleSystem
└── ObjectPool (特效对象池)
```

---

*对齐 GDD v3.0: 添加抽奖轮盘、Buff特效、分数飘字*
