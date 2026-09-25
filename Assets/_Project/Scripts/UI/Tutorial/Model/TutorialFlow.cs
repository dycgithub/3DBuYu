using System;
using System.Collections.Generic;

namespace _Project.UI.Tutorial.Model
{
    /// <summary>管理教程步骤，不依赖 Unity UI 或场景对象。</summary>
    public sealed class TutorialFlow
    {
        private IReadOnlyList<TutorialStepData> _steps = Array.Empty<TutorialStepData>();

        /// <summary>当前教程状态。</summary>
        public TutorialFlowState State { get; private set; } = TutorialFlowState.Closed;

        /// <summary>当前步骤的零基索引；未打开教程时为 -1。</summary>
        public int CurrentStepIndex { get; private set; } = -1;

        /// <summary>当前步骤；未打开教程时为空。</summary>
        public TutorialStepData CurrentStep =>
            CurrentStepIndex >= 0 && CurrentStepIndex < _steps.Count
                ? _steps[CurrentStepIndex]
                : null;

        /// <summary>步骤变化时触发，参数依次为步骤和零基索引。</summary>
        public event Action<TutorialStepData, int> StepChanged;

        /// <summary>最后一步完成时触发。</summary>
        public event Action Completed;

        /// <summary>打开教程并从第一步开始。</summary>
        /// <param name="steps">教程步骤；为空集合时不会打开。</param>
        /// <returns>成功打开时返回 true。</returns>
        public bool Open(IReadOnlyList<TutorialStepData> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return false;
            }

            _steps = steps;
            CurrentStepIndex = 0;
            State = TutorialFlowState.Showing;
            StepChanged?.Invoke(CurrentStep, CurrentStepIndex);
            return true;
        }

        /// <summary>响应下一步按钮；目标点击步骤必须由目标点击推进。</summary>
        /// <returns>发生状态变化时返回 true。</returns>
        public bool Next()
        {
            if (State != TutorialFlowState.Showing ||
                CurrentStep == null ||
                CurrentStep.AdvanceMode != TutorialAdvanceMode.NextButton)
            {
                return false;
            }

            return Advance();
        }

        /// <summary>完成当前目标点击步骤。</summary>
        /// <returns>当前步骤允许目标点击且成功推进时返回 true。</returns>
        public bool CompleteTarget()
        {
            if (State != TutorialFlowState.Showing ||
                CurrentStep == null ||
                CurrentStep.AdvanceMode != TutorialAdvanceMode.TargetClick)
            {
                return false;
            }

            return Advance();
        }

        /// <summary>推进步骤或结束当前教程。</summary>
        /// <returns>发生状态变化时返回 true。</returns>
        private bool Advance()
        {
            if (CurrentStepIndex + 1 < _steps.Count)
            {
                CurrentStepIndex++;
                StepChanged?.Invoke(CurrentStep, CurrentStepIndex);
                return true;
            }

            State = TutorialFlowState.Completed;
            Completed?.Invoke();
            return true;
        }

        /// <summary>关闭教程并清除当前步骤。</summary>
        public void Close()
        {
            _steps = Array.Empty<TutorialStepData>();
            CurrentStepIndex = -1;
            State = TutorialFlowState.Closed;
        }
    }
}
