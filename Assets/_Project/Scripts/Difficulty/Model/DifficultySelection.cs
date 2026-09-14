using System;
using System.Collections.Generic;

namespace GameSystem
{
    /// <summary>
    /// 保存本局难度并计算相邻档位。
    /// 时间门禁和配置查找集中在这里，GameManager 只负责提交已验证的切换结果。
    /// </summary>
    public sealed class DifficultySelection
    {
        /// <summary>开局后允许调整难度的时间窗口，单位为秒。</summary>
        public const float ChangeWindowSeconds = 60f;

        private readonly Dictionary<DifficultyLevel, DifficultyConfig> _options = new();

        /// <summary>当前本局难度配置。</summary>
        public DifficultyConfig Current { get; private set; }

        /// <summary>
        /// 创建难度选择状态；选项列表的顺序不影响档位关系。
        /// </summary>
        /// <param name="defaultDifficulty">本局默认难度。</param>
        /// <param name="options">可用难度配置。</param>
        public DifficultySelection(
            DifficultyConfig defaultDifficulty,
            IReadOnlyList<DifficultyConfig> options)
        {
            AddOption(defaultDifficulty);
            if (options != null)
            {
                for (int index = 0; index < options.Count; index++)
                {
                    AddOption(options[index]);
                }
            }

            Current = defaultDifficulty ?? FindFirstOption();
        }

        /// <summary>
        /// 获取指定方向的下一档难度；不改变当前选择。
        /// </summary>
        /// <param name="direction">调整方向。</param>
        /// <param name="elapsedTime">本局已运行时间。</param>
        /// <param name="target">找到的目标难度。</param>
        /// <returns>时间和配置均允许切换时返回 true。</returns>
        public bool TryGetTarget(
            DifficultyChangeDirection direction,
            float elapsedTime,
            out DifficultyConfig target)
        {
            target = null;
            if (Current == null || !IsChangeWindowOpen(elapsedTime))
            {
                return false;
            }

            int step = direction switch
            {
                DifficultyChangeDirection.Decrease => -1,
                DifficultyChangeDirection.Increase => 1,
                _ => 0
            };
            if (step == 0)
            {
                return false;
            }

            int targetValue = (int)Current.Level + step;
            if (targetValue < (int)DifficultyLevel.Easy ||
                targetValue > (int)DifficultyLevel.Impossible)
            {
                return false;
            }

            return _options.TryGetValue((DifficultyLevel)targetValue, out target) &&
                   target != null;
        }

        /// <summary>
        /// 提交已由数量服务接受的目标难度。
        /// </summary>
        /// <param name="difficulty">已生效的难度配置。</param>
        public void Commit(DifficultyConfig difficulty)
        {
            if (difficulty != null)
            {
                Current = difficulty;
            }
        }

        /// <summary>按档位顺序检查时间是否仍处于调整窗口。</summary>
        /// <param name="elapsedTime">本局已运行时间。</param>
        /// <returns>时间为有限非负值且严格小于 60 秒时返回 true。</returns>
        private static bool IsChangeWindowOpen(float elapsedTime)
        {
            return !float.IsNaN(elapsedTime) &&
                   !float.IsInfinity(elapsedTime) &&
                   elapsedTime >= 0f &&
                   elapsedTime < ChangeWindowSeconds;
        }

        /// <summary>加入首个同档位配置，避免重复配置改变当前默认选择。</summary>
        /// <param name="difficulty">待加入的配置。</param>
        private void AddOption(DifficultyConfig difficulty)
        {
            if (difficulty != null && !_options.ContainsKey(difficulty.Level))
            {
                _options.Add(difficulty.Level, difficulty);
            }
        }

        /// <summary>默认配置为空时，按从易到难找到第一个可用配置。</summary>
        /// <returns>第一个有效配置；没有配置时返回 null。</returns>
        private DifficultyConfig FindFirstOption()
        {
            for (int value = (int)DifficultyLevel.Easy;
                 value <= (int)DifficultyLevel.Impossible;
                 value++)
            {
                if (_options.TryGetValue((DifficultyLevel)value, out DifficultyConfig option))
                {
                    return option;
                }
            }

            return null;
        }
    }
}
