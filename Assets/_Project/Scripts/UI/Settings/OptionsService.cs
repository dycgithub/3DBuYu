using GameSystem;
using UnityEngine;

/// <summary>
/// 管理跨场景设置状态，并把设置立即应用到 Unity 和全局音频服务。
/// </summary>
public sealed class OptionsService
{
    private const int UncappedFrameRate = -1;
    private const int MinimumFrameRate = 30;
    private const int MaximumFrameRate = 240;

    private readonly AudioManager _audioManager;

    /// <summary>当前已加载的设置数据。</summary>
    public SettingsData Current { get; }

    /// <summary>
    /// 创建设置服务并加载已有存档。
    /// </summary>
    /// <param name="audioManager">跨场景音频服务。</param>
    public OptionsService(AudioManager audioManager)
    {
        _audioManager = audioManager;
        Current = SaveSystem.LoadSettings();
        Apply();
    }

    /// <summary>把当前设置应用到音频、窗口、垂直同步和帧率。</summary>
    public void Apply()
    {
        _audioManager?.SetMasterVolume(Current.masterVolume);
        _audioManager?.SetBGMVolume(Current.bgmVolume);
        _audioManager?.SetSFXVolume(Current.sfxVolume);

        Screen.fullScreenMode = Current.fullscreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
        Screen.fullScreen = Current.fullscreen;
        QualitySettings.vSyncCount = Current.vSync ? 1 : 0;
        Application.targetFrameRate = ResolveTargetFrameRate(
            Current.frameRateLimitEnabled,
            Current.frameRateLimit);
    }

    /// <summary>设置主音量并持久化。</summary>
    /// <param name="value">0 到 1 的音量值。</param>
    public void SetMasterVolume(float value)
    {
        Current.masterVolume = Mathf.Clamp01(value);
        _audioManager?.SetMasterVolume(Current.masterVolume);
        Save();
    }

    /// <summary>设置背景音乐音量并持久化。</summary>
    /// <param name="value">0 到 1 的音量值。</param>
    public void SetBgmVolume(float value)
    {
        Current.bgmVolume = Mathf.Clamp01(value);
        _audioManager?.SetBGMVolume(Current.bgmVolume);
        Save();
    }

    /// <summary>设置音效音量并持久化。</summary>
    /// <param name="value">0 到 1 的音量值。</param>
    public void SetSfxVolume(float value)
    {
        Current.sfxVolume = Mathf.Clamp01(value);
        _audioManager?.SetSFXVolume(Current.sfxVolume);
        Save();
    }

    /// <summary>设置窗口模式并持久化。</summary>
    /// <param name="mode">全屏或窗口模式。</param>
    public void SetWindowMode(WindowMode mode)
    {
        Current.fullscreen = mode == WindowMode.Fullscreen;
        Apply();
        Save();
    }

    /// <summary>设置垂直同步并持久化。</summary>
    /// <param name="enabled">是否启用垂直同步。</param>
    public void SetVSync(bool enabled)
    {
        Current.vSync = enabled;
        Apply();
        Save();
    }

    /// <summary>设置帧率上限开关和数值并持久化。</summary>
    /// <param name="enabled">是否启用帧率上限。</param>
    /// <param name="limit">目标帧率。</param>
    public void SetFrameRateLimit(bool enabled, int limit)
    {
        Current.frameRateLimitEnabled = enabled;
        Current.frameRateLimit = Mathf.Clamp(limit, MinimumFrameRate, MaximumFrameRate);
        Apply();
        Save();
    }

    /// <summary>设置是否在下一局战斗显示范围辅助线并持久化。</summary>
    /// <param name="enabled">是否显示战斗范围辅助线。</param>
    public void SetShowCombatRangeVisuals(bool enabled)
    {
        Current.showCombatRangeVisuals = enabled;
        Save();
    }

    /// <summary>
    /// 将设置开关和数值转换为 Unity 的目标帧率值。
    /// </summary>
    /// <param name="enabled">是否启用上限。</param>
    /// <param name="requestedLimit">请求的帧率值。</param>
    /// <returns>未限制时为 -1，否则返回限制后的帧率。</returns>
    public static int ResolveTargetFrameRate(bool enabled, int requestedLimit)
    {
        return enabled
            ? Mathf.Clamp(requestedLimit, MinimumFrameRate, MaximumFrameRate)
            : UncappedFrameRate;
    }

    private void Save()
    {
        SaveSystem.SaveSettings(Current);
    }
}

/// <summary>设置面板使用的窗口模式。</summary>
public enum WindowMode
{
    Windowed,
    Fullscreen
}
