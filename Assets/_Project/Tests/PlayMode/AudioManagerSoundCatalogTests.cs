using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Project.Tests.PlayMode
{
    /// <summary>验证音效目录覆盖音量与空间混合，同时保留现有选音和池化行为。</summary>
    public sealed class AudioManagerSoundCatalogTests
    {
        private const int AudioSampleRate = 44100;
        private const int ClipDurationSeconds = 5;
        private const float PlaybackVolumeTolerance = 0.001f;

        private readonly List<UnityEngine.Object> _objects = new();

        /// <summary>销毁测试创建的管理器、目录和音频片段。</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] is GameObject gameObject)
                {
                    gameObject.SetActive(false);
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

        /// <summary>按 Clip 覆盖每次播放的音量与空间，并在同一池音源复用时更新配置。</summary>
        [UnityTest]
        public IEnumerator ClipEntries_ApplyVolumeAndSpatialBlendToReusedSource()
        {
            AudioClip twoDimensionalClip = CreateClip("CatalogUiSound");
            AudioClip threeDimensionalClip = CreateClip("CatalogImpactSound");
            UnityEngine.Object catalog = CreateCatalog(
                ("UI hover", twoDimensionalClip, 0.25f, 0f),
                ("Projectile impact", threeDimensionalClip, 0.4f, 1f));
            _objects.Add(catalog);
            Component manager = CreateManager(catalog);

            yield return null;
            SetPlaybackVolumes(manager);

            Vector3 position = new(2f, 3f, 4f);
            Invoke(manager, "PlaySFXAtPosition", twoDimensionalClip, position, 0.8f, 1f);
            AudioSource source = GetSource(manager, twoDimensionalClip);

            Assert.That(source.volume, Is.EqualTo(0.2f).Within(PlaybackVolumeTolerance));
            Assert.That(source.spatialBlend, Is.Zero);
            Assert.That(source.transform.position, Is.EqualTo(position));

            Invoke(manager, "StopAllSFX");
            Invoke(manager, "PlaySFX", threeDimensionalClip, 0.5f, 1f);

            Assert.That(GetSource(manager, threeDimensionalClip), Is.SameAs(source));
            Assert.That(source.volume, Is.EqualTo(0.2f).Within(PlaybackVolumeTolerance));
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
        }

        /// <summary>名称、随机及转动播放入口都应用目录倍率，且转动首声不重叠。</summary>
        [UnityTest]
        public IEnumerator NamedRandomAndTurnPlayback_UseCatalogVolume()
        {
            AudioClip namedClip = CreateClip("CatalogNamedSound");
            AudioClip randomClip = CreateClip("CatalogRandomSound");
            AudioClip turnClip = CreateClip("CatalogTurnSound");
            UnityEngine.Object catalog = CreateCatalog(
                ("UI ButtonIn", namedClip, 0.5f, 0f),
                ("Random sound", randomClip, 0.25f, 0f),
                ("Diamond turn", turnClip, 0.4f, 0f));
            _objects.Add(catalog);
            Component manager = CreateManager(catalog, ("ButtonIn", namedClip));

            yield return null;
            SetPlaybackVolumes(manager);

            Invoke(manager, "PlaySFXByName", "ButtonIn", 0.6f, 1f);
            AudioSource source = GetSource(manager, namedClip);
            Assert.That(source.volume, Is.EqualTo(0.3f).Within(PlaybackVolumeTolerance));
            Assert.That(source.spatialBlend, Is.Zero);

            Invoke(manager, "StopAllSFX");
            Invoke(manager, "PlayRandomSFX", new[] { randomClip }, 0.8f);
            Assert.That(GetSource(manager, randomClip), Is.SameAs(source));
            Assert.That(source.volume, Is.EqualTo(0.2f).Within(PlaybackVolumeTolerance));

            Invoke(manager, "StopAllSFX");
            Invoke(manager, "PlayTurnSFX", turnClip);
            Invoke(manager, "PlayTurnSFX", turnClip);

            Assert.That(GetSource(manager, turnClip), Is.SameAs(source));
            Assert.That(source.volume, Is.EqualTo(0.4f).Within(PlaybackVolumeTolerance));
            Assert.That(source.spatialBlend, Is.Zero);
            Assert.That(CountSourcesUsing(manager, turnClip), Is.EqualTo(1));
        }

        /// <summary>缺目录或缺 Clip 条目时保留调用点提供的音量与二维、三维模式。</summary>
        [UnityTest]
        public IEnumerator MissingCatalogEntry_UsesCallSiteDefaults()
        {
            AudioClip fallbackClip = CreateClip("UnregisteredRuntimeSound");
            AudioClip unrelatedClip = CreateClip("UnrelatedCatalogSound");
            UnityEngine.Object catalog = CreateCatalog(
                ("Different sound", unrelatedClip, 0.1f, 0f));
            _objects.Add(catalog);
            Component catalogManager = CreateManager(catalog);
            Component managerWithoutCatalog = CreateManager(null);

            yield return null;
            SetPlaybackVolumes(catalogManager);
            SetPlaybackVolumes(managerWithoutCatalog);

            Vector3 position = new(5f, 6f, 7f);
            Invoke(catalogManager, "PlaySFXAtPosition", fallbackClip, position, 0.35f, 1f);
            AudioSource spatialSource = GetSource(catalogManager, fallbackClip);
            Assert.That(spatialSource.volume, Is.EqualTo(0.35f).Within(PlaybackVolumeTolerance));
            Assert.That(spatialSource.spatialBlend, Is.EqualTo(1f));

            Invoke(managerWithoutCatalog, "PlaySFX", fallbackClip, 0.6f, 1f);
            AudioSource flatSource = GetSource(managerWithoutCatalog, fallbackClip);
            Assert.That(flatSource.volume, Is.EqualTo(0.6f).Within(PlaybackVolumeTolerance));
            Assert.That(flatSource.spatialBlend, Is.Zero);
        }

        /// <summary>创建供本测试持有生命周期的长音频片段。</summary>
        /// <param name="clipName">片段名称。</param>
        /// <returns>五秒测试音频。</returns>
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

        /// <summary>创建包含指定 Clip 覆盖项的临时音效目录。</summary>
        /// <param name="sounds">名称、Clip、音量倍率和空间混合值。</param>
        /// <returns>临时目录 ScriptableObject。</returns>
        private static UnityEngine.Object CreateCatalog(
            params (string name, AudioClip clip, float volume, float spatialBlend)[] sounds)
        {
            Type catalogType = Type.GetType("GameSystem.SoundCatalogSO, Assembly-CSharp", true);
            UnityEngine.Object catalog = ScriptableObject.CreateInstance(catalogType);
            Type entryType = Type.GetType("GameSystem.SoundCatalogEntry, Assembly-CSharp", true);
            IList entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));

            foreach ((string name, AudioClip clip, float volume, float spatialBlend) sound in sounds)
            {
                object entry = Activator.CreateInstance(entryType);
                SetField(entry, "Name", sound.name);
                SetField(entry, "Clip", sound.clip);
                SetField(entry, "Volume", sound.volume);
                SetField(entry, "SpatialBlend", sound.spatialBlend);
                entries.Add(entry);
            }

            SetField(catalog, "_entries", entries);
            return catalog;
        }

        /// <summary>创建仅有一个池音源并可选初始化命名音效的管理器。</summary>
        /// <param name="catalog">可选目录。</param>
        /// <param name="namedClips">按原有名称入口选择的音频。</param>
        /// <returns>已启用的音频管理组件。</returns>
        private Component CreateManager(
            object catalog,
            params (string name, AudioClip clip)[] namedClips)
        {
            Type managerType = Type.GetType("GameSystem.AudioManager, Assembly-CSharp", true);
            GameObject managerObject = new("AudioManagerSoundCatalogTest");
            _objects.Add(managerObject);
            managerObject.SetActive(false);

            Component manager = managerObject.AddComponent(managerType);
            SetField(manager, "sfxPoolSize", 1);
            SetField(manager, "maximumSfxSourceCount", 2);
            SetField(manager, "sfxClips", CreateNamedClips(namedClips));
            if (catalog != null)
            {
                SetField(manager, "_soundCatalog", catalog);
            }

            managerObject.SetActive(true);
            return manager;
        }

        /// <summary>创建供 AudioManager 原有名称查找表读取的命名 Clip 列表。</summary>
        /// <param name="namedClips">名称和 Clip 配置。</param>
        /// <returns>与 AudioManager 字段类型一致的列表。</returns>
        private static object CreateNamedClips((string name, AudioClip clip)[] namedClips)
        {
            Type namedClipType = Type.GetType("GameSystem.NamedAudioClip, Assembly-CSharp", true);
            IList clips = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(namedClipType));

            foreach ((string name, AudioClip clip) namedClip in namedClips)
            {
                object entry = Activator.CreateInstance(namedClipType);
                SetField(entry, "name", namedClip.name);
                SetField(entry, "clip", namedClip.clip);
                clips.Add(entry);
            }

            return clips;
        }

        /// <summary>将全局和音效分类音量设为测试已知值。</summary>
        /// <param name="manager">待设置的音频管理器。</param>
        private static void SetPlaybackVolumes(Component manager)
        {
            Invoke(manager, "SetMasterVolume", 1f);
            Invoke(manager, "SetSFXVolume", 1f);
        }

        /// <summary>获取当前持有指定 Clip 的音源。</summary>
        /// <param name="manager">音频管理器。</param>
        /// <param name="clip">待定位的音频。</param>
        /// <returns>池内持有该 Clip 的音源。</returns>
        private static AudioSource GetSource(Component manager, AudioClip clip)
        {
            foreach (AudioSource source in manager.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip == clip)
                {
                    return source;
                }
            }

            Assert.Fail($"No pooled AudioSource contains {clip.name}.");
            return null;
        }

        /// <summary>统计池内当前引用指定片段的音源数量。</summary>
        /// <param name="manager">音频管理器。</param>
        /// <param name="clip">待计数的片段。</param>
        /// <returns>仍引用该片段的池音源数。</returns>
        private static int CountSourcesUsing(Component manager, AudioClip clip)
        {
            int count = 0;
            foreach (AudioSource source in manager.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip == clip)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>设置隔离测试所需的公开或序列化字段。</summary>
        /// <param name="target">目标对象。</param>
        /// <param name="fieldName">字段名称。</param>
        /// <param name="value">字段值。</param>
        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing test field: {fieldName}");
            field.SetValue(target, value);
        }

        /// <summary>通过 AudioManager 已有公开接口执行测试请求。</summary>
        /// <param name="target">接收请求的对象。</param>
        /// <param name="methodName">公开方法名。</param>
        /// <param name="arguments">方法参数。</param>
        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"Missing AudioManager method: {methodName}");
            method.Invoke(target, arguments);
        }
    }
}
