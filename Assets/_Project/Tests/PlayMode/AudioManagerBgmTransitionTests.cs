using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Project.Tests.PlayMode
{
    /// <summary>验证 BGM 过渡期间较早的请求不会覆盖较新的播放状态。</summary>
    public sealed class AudioManagerBgmTransitionTests
    {
        private const int AudioSampleRate = 44100;
        private const int ClipDurationSeconds = 5;
        private const float FadeOutDuration = 0.15f;
        private const float FadeInDuration = 2f;
        private const float PlaybackVolumeTolerance = 0.001f;

        private readonly List<UnityEngine.Object> _objects = new();

        /// <summary>销毁本测试创建的管理器和音频片段。</summary>
        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
        }

        /// <summary>淡出时立即停止并启动新曲后，旧淡出不能再停止新曲。</summary>
        [UnityTest]
        public IEnumerator StopWithoutFadeDuringFadeOut_KeepsNewTrackPlaying()
        {
            AudioClip firstTrack = CreateClip("FadeOutTrack");
            AudioClip latestTrack = CreateClip("LatestTrack");
            Component manager = CreateAudioManager(firstTrack, latestTrack);
            AudioSource source = GetBgmSource(manager);

            yield return null;
            SetPlaybackVolume(manager);
            PlayBgm(manager, "Menu", false);
            StopBgm(manager, true);
            StopBgm(manager, false);
            PlayBgm(manager, "Game", false);

            yield return new WaitForSecondsRealtime(FadeOutDuration * 2f);

            Assert.That(source.clip, Is.SameAs(latestTrack));
            Assert.That(source.isPlaying, Is.True);
        }

        /// <summary>淡入时快速切歌后，旧淡入不能改写新曲的播放音量。</summary>
        [UnityTest]
        public IEnumerator SwitchTrackDuringFadeIn_KeepsNewTrackVolume()
        {
            AudioClip firstTrack = CreateClip("FadeInTrack");
            AudioClip latestTrack = CreateClip("LatestTrack");
            Component manager = CreateAudioManager(firstTrack, latestTrack);
            AudioSource source = GetBgmSource(manager);

            yield return null;
            SetPlaybackVolume(manager);
            PlayBgm(manager, "Menu", true);

            yield return new WaitForSecondsRealtime(FadeOutDuration * 2f);

            Assert.That(source.clip, Is.SameAs(firstTrack));
            Assert.That(source.isPlaying, Is.True);

            PlayBgm(manager, "Game", false);
            yield return null;

            Assert.That(source.clip, Is.SameAs(latestTrack));
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.volume, Is.EqualTo(1f).Within(PlaybackVolumeTolerance));
        }

        /// <summary>创建通过临时目录配置菜单与游戏曲目的音频管理器。</summary>
        /// <param name="menuTrack">菜单曲目。</param>
        /// <param name="gameTrack">游戏曲目。</param>
        /// <returns>已激活的音频管理组件。</returns>
        private Component CreateAudioManager(AudioClip menuTrack, AudioClip gameTrack)
        {
            Type managerType = Type.GetType("GameSystem.AudioManager, Assembly-CSharp", true);
            GameObject managerObject = new("AudioManagerBgmTransitionTest");
            _objects.Add(managerObject);
            managerObject.SetActive(false);

            Component manager = managerObject.AddComponent(managerType);
            UnityEngine.Object catalog = AudioManagerBgmTestCatalog.Create(
                ("Menu", menuTrack, 1f),
                ("Game", gameTrack, 1f));
            _objects.Add(catalog);
            SetField(manager, "_musicCatalog", catalog);
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

        /// <summary>将主音量和 BGM 音量设为满值，锁定测试中的公开播放音量。</summary>
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

        /// <summary>获取音频管理器公开持有的 BGM 音源。</summary>
        /// <param name="manager">音频管理组件。</param>
        /// <returns>用于观察播放结果的音源。</returns>
        private static AudioSource GetBgmSource(Component manager)
        {
            return manager.GetComponent<AudioSource>();
        }

        /// <summary>设置音频测试夹具所需的序列化字段。</summary>
        /// <param name="target">配置所属组件。</param>
        /// <param name="fieldName">字段名称。</param>
        /// <param name="value">字段值。</param>
        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
