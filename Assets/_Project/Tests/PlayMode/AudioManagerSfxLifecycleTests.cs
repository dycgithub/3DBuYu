using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Project.Tests.PlayMode
{
    /// <summary>验证已停止的单次音效会回收到可复用的音源池，且旧租约不会重复回收。</summary>
    public sealed class AudioManagerSfxLifecycleTests
    {
        private const int AudioSampleRate = 44100;
        private const int ShortClipSampleCount = 4410;
        private const int LongClipSampleCount = AudioSampleRate * 5;
        private const int InitialSfxPoolSize = 1;
        private const int MaximumSfxSourceCount = 2;
        private const float StaleLeaseWaitSeconds = 0.2f;

        private readonly List<UnityEngine.Object> _objects = new();

        /// <summary>销毁测试创建的音频管理器和临时音效。</summary>
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

        /// <summary>停止音效后等待旧租约到期，再连续播放仍会使用独立音源。</summary>
        [UnityTest]
        public IEnumerator StopAllSfx_RecyclesSourcesWithoutDuplicateLeases()
        {
            AudioClip shortClip = CreateClip("ShortSfx", ShortClipSampleCount);
            AudioClip firstLongClip = CreateClip("FirstLongSfx", LongClipSampleCount);
            AudioClip secondLongClip = CreateClip("SecondLongSfx", LongClipSampleCount);
            Component manager = CreateManager();

            yield return null;

            PlaySfx(manager, shortClip);
            AudioSource[] sources = GetSources(manager);
            Assert.That(sources, Has.Length.EqualTo(InitialSfxPoolSize + 1));

            Invoke(manager, "StopAllSFX");
            Assert.That(sources[1].isPlaying, Is.False);
            Assert.That(sources[1].clip, Is.Null);

            yield return new WaitForSecondsRealtime(StaleLeaseWaitSeconds);

            PlaySfx(manager, firstLongClip);
            PlaySfx(manager, secondLongClip);

            Assert.That(GetSources(manager), Has.Length.EqualTo(MaximumSfxSourceCount + 1));
        }

        /// <summary>创建预分配一个音效音源的管理器。</summary>
        /// <returns>已激活的音频管理组件。</returns>
        private Component CreateManager()
        {
            System.Type managerType = System.Type.GetType("GameSystem.AudioManager, Assembly-CSharp", true);
            GameObject managerObject = new("AudioManagerSfxLifecycleTest");
            _objects.Add(managerObject);
            managerObject.SetActive(false);

            Component manager = managerObject.AddComponent(managerType);
            SetField(manager, "sfxPoolSize", InitialSfxPoolSize);
            SetField(manager, "maximumSfxSourceCount", MaximumSfxSourceCount);
            managerObject.SetActive(true);

            return manager;
        }

        /// <summary>创建并登记由测试持有生命周期的音效片段。</summary>
        /// <param name="clipName">临时音效名称。</param>
        /// <param name="sampleCount">音效样本数量。</param>
        /// <returns>供音效池播放的片段。</returns>
        private AudioClip CreateClip(string clipName, int sampleCount)
        {
            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, AudioSampleRate, false);
            _objects.Add(clip);
            return clip;
        }

        /// <summary>通过管理器公开接口播放一次二维音效。</summary>
        /// <param name="manager">音频管理组件。</param>
        /// <param name="clip">待播放片段。</param>
        private static void PlaySfx(Component manager, AudioClip clip)
        {
            Invoke(manager, "PlaySFX", clip, 1f, 1f);
        }

        /// <summary>读取管理器及其音效池持有的音源。</summary>
        /// <param name="manager">音频管理组件。</param>
        /// <returns>管理器对象及子对象上的全部音源。</returns>
        private static AudioSource[] GetSources(Component manager)
        {
            return manager.GetComponentsInChildren<AudioSource>(true);
        }

        /// <summary>设置管理器公开配置字段并调用公开生命周期接口。</summary>
        /// <param name="target">配置所属对象。</param>
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

        /// <summary>调用 AudioManager 的公开接口。</summary>
        /// <param name="target">接收调用的对象。</param>
        /// <param name="methodName">公开方法名称。</param>
        /// <param name="arguments">方法参数。</param>
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
