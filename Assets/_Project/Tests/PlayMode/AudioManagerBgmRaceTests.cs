using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Project.Tests.PlayMode
{
    /// <summary>验证最新 BGM 请求会取消尚未完成的淡变。</summary>
    public sealed class AudioManagerBgmRaceTests
    {
        private const int AudioSampleRate = 44100;
        private const int ClipDurationSeconds = 5;
        private const float FadeOutDuration = 0.15f;
        private const float FadeInDuration = 2f;
        private const float PlaybackVolumeTolerance = 0.001f;

        private readonly List<UnityEngine.Object> _objects = new();

        /// <summary>停用并延迟销毁测试对象，等待帧结束后释放登记列表。</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] is GameObject managerObject)
                {
                    managerObject.SetActive(false);
                }
            }

            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    UnityEngine.Object.Destroy(_objects[index]);
                }
            }

            yield return new WaitForEndOfFrame();
            _objects.Clear();
        }

        /// <summary>淡出尚未完成时立即停止，旧过渡不能启动目标曲目。</summary>
        [UnityTest]
        public IEnumerator StopCancelsPendingBgm()
        {
            AudioClip currentTrack = CreateClip("CurrentTrack");
            AudioClip pendingTrack = CreateClip("PendingTrack");
            Component manager = CreateManager(currentTrack, pendingTrack);
            AudioSource source = GetSource(manager);

            yield return null;
            SetPlaybackVolume(manager);
            PlayBgm(manager, "Menu", false);
            PlayBgm(manager, "Game", true);
            StopBgm(manager, false);

            yield return new WaitForSecondsRealtime(FadeOutDuration * 2f);

            Assert.That(source.clip, Is.SameAs(currentTrack));
            Assert.That(source.isPlaying, Is.False);
        }

        /// <summary>淡出时切换到即时播放的新曲，旧淡出不能停止新曲。</summary>
        [UnityTest]
        public IEnumerator SwitchCancelsFadeOutStop()
        {
            AudioClip firstTrack = CreateClip("FirstTrack");
            AudioClip latestTrack = CreateClip("LatestTrack");
            Component manager = CreateManager(firstTrack, latestTrack);
            AudioSource source = GetSource(manager);

            yield return null;
            SetPlaybackVolume(manager);
            PlayBgm(manager, "Menu", false);
            StopBgm(manager, true);
            PlayBgm(manager, "Game", false);

            yield return new WaitForSecondsRealtime(FadeOutDuration * 2f);

            Assert.That(source.clip, Is.SameAs(latestTrack));
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.volume, Is.EqualTo(1f).Within(PlaybackVolumeTolerance));
        }

        /// <summary>淡入时切换曲目，旧淡入不能覆盖新曲的即时音量。</summary>
        [UnityTest]
        public IEnumerator SwitchCancelsFadeInWrite()
        {
            AudioClip firstTrack = CreateClip("FirstTrack");
            AudioClip latestTrack = CreateClip("LatestTrack");
            Component manager = CreateManager(firstTrack, latestTrack);
            AudioSource source = GetSource(manager);

            yield return null;
            SetPlaybackVolume(manager);
            PlayBgm(manager, "Menu", false);
            PlayBgm(manager, "Game", true);

            yield return new WaitForSecondsRealtime(FadeOutDuration * 2f);

            PlayBgm(manager, "Menu", false);
            yield return new WaitForSecondsRealtime(0.05f);

            Assert.That(source.clip, Is.SameAs(firstTrack));
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.volume, Is.EqualTo(1f).Within(PlaybackVolumeTolerance));
        }

        /// <summary>创建配置了菜单与游戏曲目的音频管理器。</summary>
        /// <param name="menuTrack">菜单曲目。</param>
        /// <param name="gameTrack">游戏曲目。</param>
        /// <returns>已激活的音频管理组件。</returns>
        private Component CreateManager(AudioClip menuTrack, AudioClip gameTrack)
        {
            Type managerType = Type.GetType("GameSystem.AudioManager, Assembly-CSharp", true);
            GameObject managerObject = new("AudioManagerBgmRaceTest");
            _objects.Add(managerObject);
            managerObject.SetActive(false);

            Component manager = managerObject.AddComponent(managerType);
            SetField(manager, "menuBGM", menuTrack);
            SetField(manager, "gameBGM", gameTrack);
            SetField(manager, "bgmFadeOutTime", FadeOutDuration);
            SetField(manager, "bgmFadeInTime", FadeInDuration);
            SetField(manager, "sfxPoolSize", 0);
            managerObject.SetActive(true);

            return manager;
        }

        /// <summary>创建并登记由测试持有生命周期的音频片段。</summary>
        /// <param name="clipName">音频片段名称。</param>
        /// <returns>用于 BGM 播放的临时音频片段。</returns>
        private AudioClip CreateClip(string clipName)
        {
            AudioClip clip = AudioClip.Create(
                clipName,
                AudioSampleRate * ClipDurationSeconds,
                1,
                AudioSampleRate,
                false);
            _objects.Add(clip);
            return clip;
        }

        /// <summary>将主音量与 BGM 音量设为满值。</summary>
        /// <param name="manager">待配置的音频管理组件。</param>
        private static void SetPlaybackVolume(Component manager)
        {
            Invoke(manager, "SetMasterVolume", 1f);
            Invoke(manager, "SetBGMVolume", 1f);
        }

        /// <summary>通过 AudioManager 的公开接口切换曲目。</summary>
        /// <param name="manager">接收播放请求的音频管理组件。</param>
        /// <param name="trackName">BGM 类型名称。</param>
        /// <param name="fade">是否启用淡变。</param>
        private static void PlayBgm(Component manager, string trackName, bool fade)
        {
            Type bgmType = manager.GetType().Assembly.GetType("GameSystem.BGMType", true);
            object track = Enum.Parse(bgmType, trackName);
            Invoke(manager, "PlayBGM", track, fade);
        }

        /// <summary>通过 AudioManager 的公开接口停止曲目。</summary>
        /// <param name="manager">接收停止请求的音频管理组件。</param>
        /// <param name="fade">是否启用淡出。</param>
        private static void StopBgm(Component manager, bool fade)
        {
            Invoke(manager, "StopBGM", fade);
        }

        /// <summary>获取音频管理器持有的 BGM 音源。</summary>
        /// <param name="manager">音频管理组件。</param>
        /// <returns>用于观察播放结果的音源。</returns>
        private static AudioSource GetSource(Component manager)
        {
            return manager.GetComponent<AudioSource>();
        }

        /// <summary>设置 AudioManager 上的公开配置字段。</summary>
        /// <param name="target">配置所属组件。</param>
        /// <param name="fieldName">字段名称。</param>
        /// <param name="value">字段值。</param>
        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"找不到 AudioManager 字段：{fieldName}");
            field.SetValue(target, value);
        }

        /// <summary>调用 AudioManager 的公开方法。</summary>
        /// <param name="target">接收调用的组件。</param>
        /// <param name="methodName">公开方法名称。</param>
        /// <param name="arguments">公开方法参数。</param>
        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"找不到 AudioManager 公开方法：{methodName}");
            method.Invoke(target, arguments);
        }
    }
}
