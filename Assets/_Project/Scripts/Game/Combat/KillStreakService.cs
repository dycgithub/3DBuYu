using System;
using R3;
using Services;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 管理单局连续击杀窗口。
    /// <para>本类只保存连杀状态，不参与积分、伤害或能量计算。</para>
    /// </summary>
    public sealed class KillStreakService : IKillStreakService, IDisposable
    {
        private readonly ReactiveProperty<int> _currentStreak = new(0);
        private readonly ReactiveProperty<int> _bestStreak = new(0);

        private float _windowSeconds;
        private float _remainingSeconds;
        private bool _runStarted;
        private bool _disposed;

        /// <summary>当前未断连的击杀数。</summary>
        public ReadOnlyReactiveProperty<int> CurrentStreak => _currentStreak;

        /// <summary>本局达到过的最高连续击杀数。</summary>
        public ReadOnlyReactiveProperty<int> BestStreak => _bestStreak;

        /// <summary>
        /// 开始新的连杀记录，并清除上一局的当前值和最高值。
        /// </summary>
        /// <param name="windowSeconds">两次击杀之间允许的最大间隔，单位为秒。</param>
        public void BeginRun(float windowSeconds)
        {
            if (_disposed)
                return;

            _windowSeconds = NormalizeNonNegative(windowSeconds);
            _remainingSeconds = 0f;
            _runStarted = true;
            _currentStreak.Value = 0;
            _bestStreak.Value = 0;
        }

        /// <summary>
        /// 记录一次经过 GameManager 阶段门禁的击杀。
        /// </summary>
        /// <returns>更新后的当前连杀数；尚未开始本局时返回 0。</returns>
        public int RegisterKill()
        {
            if (_disposed || !_runStarted)
                return 0;

            int currentStreak = _currentStreak.CurrentValue + 1;
            _currentStreak.Value = currentStreak;
            if (currentStreak > _bestStreak.CurrentValue)
                _bestStreak.Value = currentStreak;

            _remainingSeconds = _windowSeconds;
            return currentStreak;
        }

        /// <summary>
        /// 推进连杀窗口；窗口耗尽只清除当前值，最高值保持到下一局。
        /// </summary>
        /// <param name="deltaTime">经过的秒数。</param>
        public void Tick(float deltaTime)
        {
            if (_disposed || !_runStarted || _currentStreak.CurrentValue <= 0)
                return;

            float normalizedDeltaTime = NormalizeNonNegative(deltaTime);
            if (normalizedDeltaTime <= 0f)
                return;

            _remainingSeconds -= normalizedDeltaTime;
            if (_remainingSeconds <= 0f)
                _currentStreak.Value = 0;
        }

        /// <summary>释放 R3 状态；服务由战斗场景作用域拥有。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _currentStreak.Dispose();
            _bestStreak.Dispose();
        }

        private static float NormalizeNonNegative(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            return Mathf.Max(0f, value);
        }
    }
}
