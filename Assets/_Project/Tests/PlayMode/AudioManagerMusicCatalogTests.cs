using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Project.Tests.PlayMode
{
    /// <summary>验证目录曲目音量在播放、淡入及暂停恢复时参与统一音量计算。</summary>
    public sealed class AudioManagerMusicCatalogTests
    {
        private const int AudioSampleRate = 44100;
        private const int ClipDurationSeconds = 2;
        private const float PlaybackVolumeTolerance = 0.001f;
        private const float PauseVolumeMultiplier = 0.3f;

        private readonly List<UnityEngine.Object> _objects = new();
        private object _pauseService;

        /// <summary>恢复测试暂停服务并销毁创建的音频对象。</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_pauseService != null)
            {
                Invoke(_pauseService, "Resume");
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
            _pauseService = null;
        }

        /// <summary>播放音量由主音量、BGM 音量及目录曲目音量相乘得到。</summary>
        [UnityTest]
        public IEnumerator PlayBgm_UsesTrackVolume()
        {
            AudioClip clip = CreateClip("CatalogGameTrack");
            UnityEngine.Object catalog = AudioManagerBgmTestCatalog.Create(("Game", clip, 0.4f));
            _objects.Add(catalog);
            Component manager = CreateManager(catalog);

            yield return null;

            SetVolume(manager, "SetMasterVolume", 0.5f);
            SetVolume(manager, "SetBGMVolume", 0.8f);
            PlayBgm(manager, "Game", false);

            AudioSource source = manager.GetComponent<AudioSource>();
            Assert.That(source.clip, Is.SameAs(clip));
            Assert.That(source.volume, Is.EqualTo(0.16f).Within(PlaybackVolumeTolerance));

            SetVolume(manager, "SetMasterVolume", 0.75f);

            Assert.That(source.volume, Is.EqualTo(0.24f).Within(PlaybackVolumeTolerance));

            SetVolume(manager, "SetBGMVolume", 0.5f);

            Assert.That(source.volume, Is.EqualTo(0.15f).Within(PlaybackVolumeTolerance));

            IList entries = (IList)catalog.GetType().GetProperty("Entries").GetValue(catalog);
            SetField(entries[0], "Volume", 0.2f);
            PlayBgm(manager, "Game", false);

            Assert.That(source.volume, Is.EqualTo(0.075f).Within(PlaybackVolumeTolerance));
        }

        /// <summary>目录未配置的 BGM 类型不会启动音频。</summary>
        [UnityTest]
        public IEnumerator UnknownTrack_DoesNotPlayBgm()
        {
            AudioClip clip = CreateClip("ConfiguredGameTrack");
            UnityEngine.Object catalog = AudioManagerBgmTestCatalog.Create(("Game", clip, 1f));
            _objects.Add(catalog);
            Component manager = CreateManager(catalog);

            yield return null;

            PlayBgm(manager, "Battle", false);

            AudioSource source = manager.GetComponent<AudioSource>();
            Assert.That(source.clip, Is.Null);
            Assert.That(source.isPlaying, Is.False);
        }

        /// <summary>同一 BGM 类型的曲目资源变更会应用新片段。</summary>
        [UnityTest]
        public IEnumerator SameTrackType_UsesChangedClip()
        {
            AudioClip firstClip = CreateClip("OriginalGameTrack");
            AudioClip updatedClip = CreateClip("UpdatedGameTrack");
            UnityEngine.Object catalog = AudioManagerBgmTestCatalog.Create(("Game", firstClip, 1f));
            _objects.Add(catalog);
            Component manager = CreateManager(catalog);

            yield return null;

            PlayBgm(manager, "Game", false);
            IList entries = (IList)catalog.GetType().GetProperty("Entries").GetValue(catalog);
            SetField(entries[0], "Clip", updatedClip);
            PlayBgm(manager, "Game", false);

            AudioSource source = manager.GetComponent<AudioSource>();
            Assert.That(source.clip, Is.SameAs(updatedClip));
            Assert.That(source.isPlaying, Is.True);
        }

        /// <summary>淡入终点采用目录曲目音量及当前暂停倍率。</summary>
        [UnityTest]
        public IEnumerator FadeAndPause_UseTrackVolume()
        {
            AudioClip clip = CreateClip("CatalogFadeTrack");
            UnityEngine.Object catalog = AudioManagerBgmTestCatalog.Create(("Game", clip, 0.4f));
            _objects.Add(catalog);
            Component manager = CreateManager(catalog);
            SetField(manager, "bgmFadeOutTime", 0.01f);
            SetField(manager, "bgmFadeInTime", 0.05f);

            yield return null;

            SetVolume(manager, "SetMasterVolume", 0.5f);
            SetVolume(manager, "SetBGMVolume", 0.8f);
            PlayBgm(manager, "Game", true);

            yield return new WaitForSecondsRealtime(0.2f);

            AudioSource source = manager.GetComponent<AudioSource>();
            Assert.That(source.volume, Is.EqualTo(0.16f).Within(PlaybackVolumeTolerance));

            _pauseService = CreatePauseService();
            Invoke(manager, "BindPauseService", _pauseService);
            Invoke(_pauseService, "Pause");

            Assert.That(
                source.volume,
                Is.EqualTo(0.16f * PauseVolumeMultiplier).Within(PlaybackVolumeTolerance));

            Invoke(_pauseService, "Resume");

            Assert.That(source.volume, Is.EqualTo(0.16f).Within(PlaybackVolumeTolerance));
        }

        /// <summary>创建并登记由测试管理生命周期的临时音频片段。</summary>
        /// <param name="clipName">临时片段名称。</param>
        /// <returns>生成的音频片段。</returns>
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

        /// <summary>创建并启用引用指定目录的音频管理器。</summary>
        /// <param name="catalog">该管理器使用的曲目目录。</param>
        /// <returns>已启用的音频管理组件。</returns>
        private Component CreateManager(object catalog)
        {
            Type managerType = Type.GetType("GameSystem.AudioManager, Assembly-CSharp", true);
            GameObject managerObject = new("AudioManagerMusicCatalogTest");
            _objects.Add(managerObject);
            managerObject.SetActive(false);

            Component manager = managerObject.AddComponent(managerType);
            SetField(manager, "_musicCatalog", catalog);
            SetField(manager, "sfxPoolSize", 0);
            managerObject.SetActive(true);
            return manager;
        }

        /// <summary>通过现有暂停服务类型创建测试暂停源。</summary>
        /// <returns>实现游戏暂停接口的服务对象。</returns>
        private static object CreatePauseService()
        {
            Type pauseType = Type.GetType("GameSystem.GamePauseService, Assembly-CSharp", true);
            return Activator.CreateInstance(pauseType);
        }

        /// <summary>通过 AudioManager 的公开接口设置分类音量。</summary>
        /// <param name="manager">待设置的音频管理器。</param>
        /// <param name="methodName">音量设置方法名称。</param>
        /// <param name="volume">新音量。</param>
        private static void SetVolume(Component manager, string methodName, float volume)
        {
            Invoke(manager, methodName, volume);
        }

        /// <summary>通过 AudioManager 的公开接口提交 BGM 请求。</summary>
        /// <param name="manager">接收请求的音频管理器。</param>
        /// <param name="id">曲目类型名称。</param>
        /// <param name="fade">是否执行淡变。</param>
        private static void PlayBgm(Component manager, string id, bool fade)
        {
            Invoke(manager, "PlayBGM", Enum.Parse(GetBgmType(), id), fade);
        }

        /// <summary>获取项目中已存在的 BGM 类型。</summary>
        /// <returns>项目定义的 BGM 枚举类型。</returns>
        private static Type GetBgmType()
        {
            return Type.GetType("GameSystem.BGMType, Assembly-CSharp", true);
        }

        /// <summary>设置测试专用序列化字段。</summary>
        /// <param name="target">被设置的对象。</param>
        /// <param name="fieldName">字段名称。</param>
        /// <param name="value">字段值。</param>
        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"找不到字段：{fieldName}");
            field.SetValue(target, value);
        }

        /// <summary>调用测试对象的公开接口或现有暂停服务方法。</summary>
        /// <param name="target">接收调用的对象。</param>
        /// <param name="methodName">方法名称。</param>
        /// <param name="arguments">方法参数。</param>
        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"找不到方法：{methodName}");
            method.Invoke(target, arguments);
        }
    }
}
