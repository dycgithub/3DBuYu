using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSystem
{
    public class TimeManager
    {
        public float TotalTime { get; private set; }
        public float RemainingTime { get; private set; }
        public bool IsExpired => RemainingTime <= 0f;
        public bool IsPaused { get; private set; }

        /// <summary>本次归零是否由时间惩罚导致(true = 惩罚扣光,false = 自然倒计时耗尽)。</summary>
        public bool ExpiredByPenalty { get; private set; }

        public event Action<float> OnTimeChanged;
        public event Action OnTimeExpired;

        public float RewardMultiplier { get; set; } = 1f;
        public float PenaltyMultiplier { get; set; } = 1f;

        private bool wasExpired;

        public void Initialize(float totalTime)
        {
            TotalTime = totalTime;
            RemainingTime = totalTime;
            IsPaused = false;
            wasExpired = false;
            ExpiredByPenalty = false;
        }

        public void Reset(float totalTime)
        {
            TotalTime = totalTime;
            RemainingTime = totalTime;
            IsPaused = false;
            wasExpired = false;
            ExpiredByPenalty = false;
        }

        public void Tick(float deltaTime)
        {
            if (IsPaused) return;

            RemainingTime -= deltaTime;
            OnTimeChanged?.Invoke(RemainingTime);

            if (!wasExpired && RemainingTime <= 0f)
            {
                wasExpired = true;
                ExpiredByPenalty = false;
                TryExtendTime();
                if (RemainingTime <= 0f)
                    OnTimeExpired?.Invoke();
            }
        }

        public void AddTime(float seconds)
        {
            if (seconds <= 0f) return;
            float actual = seconds * RewardMultiplier;
            RemainingTime += actual;
            if (RemainingTime > 0f) wasExpired = false;
            OnTimeChanged?.Invoke(RemainingTime);
        }

        public void AddTimePenalty(float seconds)
        {
            if (seconds <= 0f) return;
            float actual = seconds * PenaltyMultiplier;
            RemainingTime -= actual;
            OnTimeChanged?.Invoke(RemainingTime);

            if (!wasExpired && RemainingTime <= 0f)
            {
                wasExpired = true;
                ExpiredByPenalty = true;
                TryExtendTime();
                if (RemainingTime <= 0f)
                    OnTimeExpired?.Invoke();
            }
        }

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
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
                    RemainingTime += extra;
                    wasExpired = false;
                    OnTimeChanged?.Invoke(RemainingTime);
                    break;
                }
            }
        }
    }
}
