# 音频系统 (Audio System) 规格说明书

## 模块概述

音频系统负责管理游戏中的所有声音播放，包括背景音乐(BGM)和音效(SFX)。系统支持音量控制、淡入淡出、音频池管理等功能。

## 架构设计

### 核心组件

```
┌─────────────────────────────────────────────────────────────┐
│                    AudioManager                             │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────┐   │
│  │  BGM管理 (背景音乐)                                  │   │
│  │  - 播放/暂停/停止                                   │   │
│  │  - 淡入淡出切换                                     │   │
│  │  - 循环播放                                         │   │
│  └─────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  SFX管理 (音效)                                      │   │
│  │  - 音频池管理                                       │   │
│  │  - 3D空间音效                                       │   │
│  │  - 随机变调                                         │   │
│  └─────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  音量控制                                           │   │
│  │  - 主音量                                           │   │
│  │  - BGM音量                                          │   │
│  │  - SFX音量                                          │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### BGM类型枚举

```csharp
public enum BGMType
{
    None,       // 无
    Menu,       // 主菜单
    Game,       // 游戏中
    Battle,     // 战斗
    Victory,    // 胜利
    GameOver    // 失败
}
```

## 核心功能规格

### 1. 背景音乐管理

| 方法 | 参数 | 说明 |
|------|------|------|
| `PlayBGM(BGMType, bool fade)` | type, fade | 播放指定BGM |
| `StopBGM(bool fade)` | fade | 停止当前BGM |
| `PauseBGM()` | - | 暂停BGM |
| `ResumeBGM()` | - | 恢复BGM |

**淡入淡出实现**:

```csharp
private IEnumerator FadeBGM(AudioClip newClip)
{
    // 淡出
    float startVolume = bgmSource.volume;
    float elapsed = 0f;

    while (elapsed < bgmFadeOutTime)
    {
        elapsed += Time.deltaTime;
        bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / bgmFadeOutTime);
        yield return null;
    }

    // 切换
    bgmSource.Stop();
    bgmSource.clip = newClip;
    bgmSource.Play();

    // 淡入
    elapsed = 0f;
    while (elapsed < bgmFadeInTime)
    {
        elapsed += Time.deltaTime;
        bgmSource.volume = Mathf.Lerp(0f, BGMVolume, elapsed / bgmFadeInTime);
        yield return null;
    }

    bgmSource.volume = BGMVolume;
}
```

### 2. 音效管理

| 方法 | 参数 | 说明 |
|------|------|------|
| `PlaySFX(AudioClip, float, float)` | clip, volume, pitch | 播放音效 |
| `PlaySFXByName(string, float, float)` | name, volume, pitch | 通过名称播放 |
| `PlaySFXAtPosition(AudioClip, Vector3, float)` | clip, position, volume | 在位置播放 |
| `PlayRandomSFX(AudioClip[], float)` | clips, volume | 随机播放 |

**音效池实现**:

```csharp
private AudioSource GetSFXSource()
{
    // 优先从池获取
    if (sfxPool.Count > 0)
    {
        return sfxPool.Dequeue();
    }

    // 找一个已停止的
    foreach (var source in sfxSources)
    {
        if (!source.isPlaying)
        {
            return source;
        }
    }

    // 创建新的
    AudioSource newSource = gameObject.AddComponent<AudioSource>();
    newSource.playOnAwake = false;
    sfxSources.Add(newSource);
    return newSource;
}

private IEnumerator ReturnSFXSource(AudioSource source, float delay)
{
    yield return new WaitForSeconds(delay);

    if (!sfxPool.Contains(source))
    {
        sfxPool.Enqueue(source);
    }
}
```

### 3. 音量控制

| 属性 | 类型 | 范围 | 说明 |
|------|------|------|------|
| `MasterVolume` | float | 0-1 | 主音量 |
| `BGMVolume` | float | 0-1 | 背景音乐音量 |
| `SFXVolume` | float | 0-1 | 音效音量 |

**分贝转换**:

```csharp
private float LinearToDecibel(float linear)
{
    return linear > 0 ? Mathf.Log10(linear) * 20 : -80f;
}

public void SetMasterVolume(float volume)
{
    MasterVolume = Mathf.Clamp01(volume);

    if (mainMixer != null)
    {
        float db = LinearToDecibel(MasterVolume);
        mainMixer.SetFloat(masterVolumeParam, db);
    }

    bgmSource.volume = MasterVolume * BGMVolume;
}
```

## 接口定义

### 公共方法

```csharp
// BGM控制
public void PlayBGM(BGMType type, bool fade = true)
public void StopBGM(bool fade = true)
public void PauseBGM()
public void ResumeBGM()

// SFX播放
public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
public void PlaySFXByName(string name, float volume = 1f, float pitch = 1f)
public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
public void PlayRandomSFX(AudioClip[] clips, float volume = 1f)

// 音量控制
public void SetMasterVolume(float volume)
public void SetBGMVolume(float volume)
public void SetSFXVolume(float volume)
public void ToggleMute()

// 设置持久化
public void SaveSettings()
public void LoadSettings()
```

### 事件

```csharp
/// <summary>
/// BGM改变事件
/// </summary>
public event Action<BGMType> OnBGMChanged;

/// <summary>
/// 主音量改变事件
/// </summary>
public event Action<float> OnMasterVolumeChanged;

/// <summary>
/// BGM音量改变事件
/// </summary>
public event Action<float> OnBGMVolumeChanged;

/// <summary>
/// SFX音量改变事件
/// </summary>
public event Action<float> OnSFXVolumeChanged;

/// <summary>
/// 静音状态改变事件
/// </summary>
public event Action<bool> OnMuteChanged;
```

## 配置示例

```json
{
  "audio": {
    "bgm": {
      "menu": "Audio/BGM/MenuTheme",
      "game": "Audio/BGM/GameTheme",
      "battle": "Audio/BGM/BattleTheme",
      "victory": "Audio/BGM/VictoryTheme",
      "gameOver": "Audio/BGM/GameOverTheme"
    },
    "sfx": {
      "shoot": "Audio/SFX/Shoot",
      "explosion": "Audio/SFX/Explosion",
      "hit": "Audio/SFX/Hit",
      "coin": "Audio/SFX/Coin",
      "powerup": "Audio/SFX/PowerUp"
    },
    "settings": {
      "masterVolume": 1.0,
      "bgmVolume": 0.8,
      "sfxVolume": 1.0,
      "bgmFadeInTime": 2.0,
      "bgmFadeOutTime": 1.0,
      "sfxPoolSize": 10
    }
  }
}
```

## 依赖关系

```
AudioSystem
├── Unity Audio (Unity内置音频系统)
├── Unity AudioMixer (混音器)
└── SaveSystem (设置持久化)
```

## 测试要点

1. **功能测试**: 各种BGM切换、SFX播放、音量控制
2. **性能测试**: 大量SFX同时播放、音频池溢出处理
3. **边界测试**: 静音切换、快速BGM切换、无效音频文件
4. **跨平台测试**: 不同平台的音频格式支持
