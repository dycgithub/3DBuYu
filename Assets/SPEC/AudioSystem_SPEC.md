# 音频系统 (Audio System) 规格说明书

## 模块概述

音频系统负责所有声音播放：BGM（背景音乐）和 SFX（音效）。支持音量控制、淡入淡出、音频池管理。

## GDD 音频需求

| 事件 | 音频 |
|------|------|
| 基础弹命中 | 命中音效 |
| 高级弹命中 | 高级命中音效（穿透/爆炸/毒液各不同） |
| 击杀敌人 | 击杀音效 |
| 击杀黄金鱼 | 特殊音效 |
| 轨道/球体扩展 | 升级音效 |
| 购买升级 | UI音效 |
| 子弹切换 | 切换音效 |
| Buff激活 | Buff音效 |
| 抽奖轮盘 | 轮盘旋转音效 + 中奖音效 |
| 通关 | 胜利BGM |
| 失败 | 失败BGM |
| 倒计时最后10秒 | 心跳倒计时音效 |

## BGM类型

| BGM | 说明 |
|------|------|
| `Menu` | 主菜单 |
| `Game` | 游戏中 |
| `Boss` | BOSS关 |
| `LastTenSeconds` | 倒计时最后10秒 |
| `Victory` | 通关 |
| `GameOver` | 失败 |

## 接口

```csharp
// BGM
public void PlayBGM(BGMType type, bool fade = true);
public void StopBGM(bool fade = true);

// SFX
public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f);
public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f);

// 音量
public void SetMasterVolume(float volume);
public void SetBGMVolume(float volume);
public void SetSFXVolume(float volume);

// 事件
public event Action<BGMType> OnBGMChanged;
```

## 依赖关系

```
AudioSystem
├── Unity Audio + AudioMixer
└── SaveSystem (音量设置持久化)
```

---

*对齐 GDD v3.0: 添加Boss/倒计时/Buff/抽奖音效*
