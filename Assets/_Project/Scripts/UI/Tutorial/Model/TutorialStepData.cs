using System;
using UnityEngine;

namespace _Project.UI.Tutorial.Model
{
    /// <summary>保存一个教程步骤的文字、目标和完成规则。</summary>
    [Serializable]
    public sealed class TutorialStepData
    {
        [SerializeField] private string _title;
        [SerializeField, TextArea(2, 6)] private string _description;
        [SerializeField] private string _targetId;
        [SerializeField] private TutorialFocusMode _focusMode = TutorialFocusMode.FullScreen;
        [SerializeField] private TutorialAdvanceMode _advanceMode = TutorialAdvanceMode.NextButton;
        [SerializeField] private TutorialPanelPlacement _panelPlacement = TutorialPanelPlacement.Auto;
        [SerializeField] private Vector2 _focusPadding = new(24f, 24f);

        /// <summary>供 Unity 序列化使用的默认构造函数。</summary>
        public TutorialStepData()
        {
        }

        /// <summary>供 EditMode 测试和代码创建步骤使用的构造函数。</summary>
        public TutorialStepData(
            string title,
            string description,
            string targetId,
            TutorialAdvanceMode advanceMode)
        {
            _title = title;
            _description = description;
            _targetId = targetId;
            _advanceMode = advanceMode;
            _focusMode = string.IsNullOrWhiteSpace(targetId)
                ? TutorialFocusMode.FullScreen
                : TutorialFocusMode.Target;
        }

        public string Title => _title;
        public string Description => _description;
        public string TargetId => _targetId;
        public TutorialFocusMode FocusMode => _focusMode;
        public TutorialAdvanceMode AdvanceMode => _advanceMode;
        public TutorialPanelPlacement PanelPlacement => _panelPlacement;
        public Vector2 FocusPadding => _focusPadding;
    }
}
