using System;
using System.Collections.Generic;
using EnemySystem.Components;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace EnemySystem.AreaEffect
{
    /// <summary>
    /// 统一驱动场景内的敌人范围效果源。
    /// <para>组件只保存自身状态，服务负责把每个范围源纳入同一个 PlayerLoop。</para>
    /// <para>服务不执行空间查询，查询和目标节流仍由具体范围源负责。</para>
    /// </summary>
    public sealed class EnemyAreaEffectService : ITickable, IDisposable
    {
        private const int MaximumSourcesPerFrame = 256;

        private static readonly ProfilerMarker TickMarker =
            new("3DBuYu/VContainer/EnemyAreaEffectService.Tick");
        private static readonly ProfilerMarker ProcessMarker =
            new("3DBuYu/EnemyAreaEffectService.Process");

        private readonly List<EnemyAreaEffectComponent> _sources = new();
        private readonly List<float> _sourceProcessTimes = new();
        private readonly Dictionary<EnemyAreaEffectComponent, int> _sourceIndices = new();
        private float _elapsedTime;
        private int _nextSourceIndex;

        /// <summary>注册一个活动范围效果源，重复注册会被忽略。</summary>
        /// <param name="source">待注册的范围效果组件。</param>
        public void Register(EnemyAreaEffectComponent source)
        {
            if (source == null || _sourceIndices.ContainsKey(source))
            {
                return;
            }

            _sourceIndices.Add(source, _sources.Count);
            _sources.Add(source);
            _sourceProcessTimes.Add(_elapsedTime);
        }

        /// <summary>注销一个范围效果源。</summary>
        /// <param name="source">待注销的范围效果组件。</param>
        public void Unregister(EnemyAreaEffectComponent source)
        {
            if (source != null)
            {
                if (_sourceIndices.TryGetValue(source, out int index))
                {
                    RemoveAt(index);
                }
            }
        }

        /// <summary>由 VContainer PlayerLoop 按帧推进所有活动范围效果源。</summary>
        public void Tick()
        {
            using var profilerScope = TickMarker.Auto();
            Process(Time.deltaTime);
        }

        /// <summary>
        /// 按给定帧间隔轮转推进范围效果。
        /// 每帧只处理固定数量的范围源，未处理源累计经过时间，避免高人口时整表扫描。
        /// </summary>
        /// <param name="deltaTime">当前帧经过的秒数。</param>
        internal void Process(float deltaTime)
        {
            using var profilerScope = ProcessMarker.Auto();
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            _elapsedTime += safeDeltaTime;

            int processedCount = 0;
            while (processedCount < MaximumSourcesPerFrame && _sources.Count > 0)
            {
                if (_nextSourceIndex >= _sources.Count)
                {
                    _nextSourceIndex = 0;
                }

                int index = _nextSourceIndex;
                EnemyAreaEffectComponent source = _sources[index];
                if (source == null || !source.isActiveAndEnabled)
                {
                    RemoveAt(index);
                    continue;
                }

                _nextSourceIndex = (index + 1) % _sources.Count;
                float elapsed = Mathf.Max(0f, _elapsedTime - _sourceProcessTimes[index]);
                _sourceProcessTimes[index] = _elapsedTime;
                source.ProcessFrame(elapsed);
                processedCount++;
            }
        }

        /// <summary>场景容器销毁时解除范围源引用。</summary>
        public void Dispose()
        {
            _sources.Clear();
            _sourceProcessTimes.Clear();
            _sourceIndices.Clear();
            _nextSourceIndex = 0;
            _elapsedTime = 0f;
        }

        private void RemoveAt(int index)
        {
            int lastIndex = _sources.Count - 1;
            EnemyAreaEffectComponent removedSource = _sources[index];
            if (index != lastIndex)
            {
                EnemyAreaEffectComponent movedSource = _sources[lastIndex];
                _sources[index] = movedSource;
                _sourceProcessTimes[index] = _sourceProcessTimes[lastIndex];
                _sourceIndices[movedSource] = index;
            }

            _sources.RemoveAt(lastIndex);
            _sourceProcessTimes.RemoveAt(lastIndex);
            _sourceIndices.Remove(removedSource);

            if (_sources.Count == 0)
            {
                _nextSourceIndex = 0;
                return;
            }

            if (_nextSourceIndex > index)
            {
                _nextSourceIndex--;
            }

            if (_nextSourceIndex >= _sources.Count)
            {
                _nextSourceIndex = 0;
            }
        }
    }
}
