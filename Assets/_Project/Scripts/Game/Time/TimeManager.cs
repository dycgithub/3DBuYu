using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace GameSystem
{
    public class TimeManager : IDisposable
    {
        public float TotalTime { get; private set; }

        /// <summary>剩余时间(可观察:R3,订阅即得当前值,每帧变化实时推送)。</summary>
        public ReadOnlyReactiveProperty<float> RemainingTime => _remainingTime;

        public bool IsExpired => _remainingTime.CurrentValue <= 0f;

        /// <summary>本次归零是否由时间惩罚导致(true = 惩罚扣光,false = 自然倒计时耗尽)。</summary>
        public bool ExpiredByPenalty { get; private set; }

        /// <summary>剩余时间归零时触发的一次性事实通知，不代表胜负。</summary>
        public event Action OnTimeExpired;

        public float RewardMultiplier { get; set; } = 1f;
        public float PenaltyMultiplier { get; set; } = 1f;

        private readonly ReactiveProperty<float> _remainingTime = new();
        private bool wasExpired;

        public void Initialize(float totalTime)
        {
            TotalTime = totalTime;
            _remainingTime.Value = totalTime;
            wasExpired = false;
            ExpiredByPenalty = false;
        }

        public void Reset(float totalTime)
        {
            TotalTime = totalTime;
            _remainingTime.Value = totalTime;
            wasExpired = false;
            ExpiredByPenalty = false;
        }

        public void Tick(float deltaTime)
        {
            _remainingTime.Value -= deltaTime;

            if (!wasExpired && _remainingTime.CurrentValue <= 0f)
            {
                wasExpired = true;
                ExpiredByPenalty = false;
                TryExtendTime();
                if (_remainingTime.CurrentValue <= 0f)
                    OnTimeExpired?.Invoke();
            }
        }

        public void AddTime(float seconds)
        {
            if (seconds <= 0f) return;
            float actual = seconds * RewardMultiplier;
            _remainingTime.Value += actual;
            if (_remainingTime.CurrentValue > 0f) wasExpired = false;
        }

        public void AddTimePenalty(float seconds)
        {
            if (seconds <= 0f) return;
            float actual = seconds * PenaltyMultiplier;
            _remainingTime.Value -= actual;

            if (!wasExpired && _remainingTime.CurrentValue <= 0f)
            {
                wasExpired = true;
                ExpiredByPenalty = true;
                TryExtendTime();
                if (_remainingTime.CurrentValue <= 0f)
                    OnTimeExpired?.Invoke();
            }
        }

        public void RegisterExtension(ITimeExtension extension)
        {
            if (extension == null || extensions.Contains(extension)) return;
            extensions.Add(extension);
            extensions.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void UnregisterExtension(ITimeExtension extension) => extensions.Remove(extension);

        private readonly List<ITimeExtension> extensions = new();

        private void TryExtendTime()
        {
            foreach (var ext in extensions)
            {
                float extra = ext.GetExtraTime();
                if (extra > 0f)
                {
                    _remainingTime.Value += extra;
                    wasExpired = false;
                    break;
                }
            }
        }

        public void Dispose() => _remainingTime.Dispose();
    }
}
