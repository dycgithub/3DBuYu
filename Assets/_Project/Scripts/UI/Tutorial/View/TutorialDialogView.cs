using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using _Project.UI.Animations;
using _Project.UI.Tutorial.Model;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace _Project.UI.Tutorial.View
{
    /// <summary>显示当前语言教程文字，语言切换只刷新文字，保留步骤和弹窗位置。</summary>
    public sealed class TutorialDialogView : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private RectTransform _dialogRect;
        [SerializeField] private RectTransform _placementSpace;
        [SerializeField] private UIPanelShowHide _panelAnimation;
        [SerializeField] private PanelDragHandle _dragHandle;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _stepCounter;
        [SerializeField] private TMP_Text _nextLabel;
        [SerializeField] private UnityEngine.UI.Button _nextButton;
        [SerializeField] private UnityEngine.UI.Button _closeButton;

        [Header("布局")]
        [SerializeField, Min(0f)] private float _placementGap = 32f;

        private Vector2 _defaultPosition;
        private bool _userPosition;
        private TutorialStepData _currentStep;
        private int _currentIndex;
        private int _stepTotal;

        /// <summary>点击下一步时触发。</summary>
        public event Action NextPressed;

        /// <summary>点击关闭时触发。</summary>
        public event Action ClosePressed;

        /// <summary>弹窗 RectTransform。</summary>
        public RectTransform DialogRect => _dialogRect;

        /// <summary>初始化引用、按钮和默认位置。</summary>
        private void Awake()
        {
            _dialogRect ??= transform as RectTransform;
            _placementSpace ??= _dialogRect?.parent as RectTransform;
            _panelAnimation ??= GetComponent<UIPanelShowHide>();
            _dragHandle ??= GetComponentInChildren<PanelDragHandle>(true);
            _defaultPosition = _dialogRect != null ? _dialogRect.anchoredPosition : Vector2.zero;

            _nextButton?.onClick.AddListener(HandleNextPressed);
            _closeButton?.onClick.AddListener(HandleClosePressed);
            LocalizationSettings.SelectedLocaleChanged += RefreshLanguage;
            if (_dragHandle != null)
            {
                _dragHandle.PositionChanged += HandlePositionChanged;
            }
        }

        /// <summary>释放按钮和拖动位置监听。</summary>
        private void OnDestroy()
        {
            _nextButton?.onClick.RemoveListener(HandleNextPressed);
            _closeButton?.onClick.RemoveListener(HandleClosePressed);
            LocalizationSettings.SelectedLocaleChanged -= RefreshLanguage;
            if (_dragHandle != null)
            {
                _dragHandle.PositionChanged -= HandlePositionChanged;
            }
        }

        /// <summary>刷新文字并显示弹窗。</summary>
        /// <param name="step">当前教程步骤。</param>
        /// <param name="index">当前步骤的零基索引。</param>
        /// <param name="total">教程步骤总数。</param>
        public void ShowStep(TutorialStepData step, int index, int total)
        {
            if (step == null)
            {
                return;
            }

            _currentStep = step;
            _currentIndex = index;
            _stepTotal = total;
            RefreshLanguage(LocalizationSettings.SelectedLocale);
            if (_nextButton != null)
            {
                _nextButton.interactable = step.AdvanceMode == TutorialAdvanceMode.NextButton;
            }

            if (_panelAnimation != null)
            {
                _panelAnimation.Show();
            }
        }

        /// <summary>刷新当前步骤译文，不推进教程、不重播显示动画。</summary>
        private void RefreshLanguage(Locale _)
        {
            if (_currentStep == null)
            {
                return;
            }

            if (_title != null)
            {
                _title.text = _currentStep.Title;
            }
            if (_description != null)
            {
                _description.text = _currentStep.Description;
            }
            if (_stepCounter != null)
            {
                _stepCounter.text = $"{_currentIndex + 1} / {Mathf.Max(1, _stepTotal)}";
            }
            if (_nextLabel != null)
            {
                _nextLabel.text = L10n.Get(_currentIndex + 1 >= _stepTotal ? "ui.quit" : "ui.next");
            }
        }

        /// <summary>按目标位置自动摆放弹窗；用户拖动后不再覆盖手动位置。</summary>
        /// <param name="placement">配置的摆放方向。</param>
        /// <param name="focusRect">高亮目标在同一坐标空间内的矩形。</param>
        /// <param name="focusMode">当前步骤是否显示目标高亮。</param>
        public void Place(
            TutorialPanelPlacement placement,
            Rect focusRect,
            TutorialFocusMode focusMode)
        {
            if (_dialogRect == null || _placementSpace == null || _userPosition ||
                focusMode != TutorialFocusMode.Target)
            {
                return;
            }

            Rect bounds = _placementSpace.rect;
            Vector2 position = placement == TutorialPanelPlacement.Auto
                ? FindAutomaticPosition(focusRect, bounds)
                : FindPosition(placement, focusRect, bounds);
            _dialogRect.anchoredPosition = ClampPosition(position, bounds);
        }

        /// <summary>重置弹窗位置，开始一部教程时使用。</summary>
        public void ResetPosition()
        {
            _userPosition = false;
            if (_dialogRect != null)
            {
                _dialogRect.anchoredPosition = _defaultPosition;
            }
        }

        /// <summary>设置当前高亮区域为弹窗拖动禁入区域。</summary>
        /// <param name="focusRect">高亮目标在弹窗父级坐标系内的矩形。</param>
        public void SetForbiddenRect(Rect focusRect)
        {
            _dragHandle?.SetForbiddenRect(focusRect);
        }

        /// <summary>清除弹窗拖动禁入区域。</summary>
        public void ClearForbiddenRect()
        {
            _dragHandle?.ClearForbiddenRect();
        }

        /// <summary>隐藏教程弹窗。</summary>
        public void Hide()
        {
            _panelAnimation?.Hide();
        }

        /// <summary>根据可用空间选择最合适的目标侧边。</summary>
        private Vector2 FindAutomaticPosition(Rect focus, Rect bounds)
        {
            float left = focus.xMin - bounds.xMin;
            float right = bounds.xMax - focus.xMax;
            float top = bounds.yMax - focus.yMax;
            float bottom = focus.yMin - bounds.yMin;
            float maximum = Mathf.Max(Mathf.Max(left, right), Mathf.Max(top, bottom));

            if (maximum == top)
            {
                return FindPosition(TutorialPanelPlacement.Top, focus, bounds);
            }
            if (maximum == bottom)
            {
                return FindPosition(TutorialPanelPlacement.Bottom, focus, bounds);
            }
            if (maximum == left)
            {
                return FindPosition(TutorialPanelPlacement.Left, focus, bounds);
            }
            return FindPosition(TutorialPanelPlacement.Right, focus, bounds);
        }

        /// <summary>根据指定侧边计算弹窗中心位置。</summary>
        private Vector2 FindPosition(
            TutorialPanelPlacement placement,
            Rect focus,
            Rect bounds)
        {
            float halfWidth = _dialogRect.rect.width * 0.5f;
            float halfHeight = _dialogRect.rect.height * 0.5f;
            return placement switch
            {
                TutorialPanelPlacement.Top => new Vector2(
                    focus.center.x,
                    focus.yMax + _placementGap + halfHeight),
                TutorialPanelPlacement.Bottom => new Vector2(
                    focus.center.x,
                    focus.yMin - _placementGap - halfHeight),
                TutorialPanelPlacement.Left => new Vector2(
                    focus.xMin - _placementGap - halfWidth,
                    focus.center.y),
                TutorialPanelPlacement.Right => new Vector2(
                    focus.xMax + _placementGap + halfWidth,
                    focus.center.y),
                _ => bounds.center
            };
        }

        /// <summary>将弹窗中心限制在父级矩形内。</summary>
        private Vector2 ClampPosition(Vector2 position, Rect bounds)
        {
            float halfWidth = _dialogRect.rect.width * 0.5f;
            float halfHeight = _dialogRect.rect.height * 0.5f;
            return new Vector2(
                Mathf.Clamp(position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth),
                Mathf.Clamp(position.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight));
        }

        /// <summary>转发下一步按钮事件。</summary>
        private void HandleNextPressed()
        {
            NextPressed?.Invoke();
        }

        /// <summary>转发关闭按钮事件。</summary>
        private void HandleClosePressed()
        {
            ClosePressed?.Invoke();
        }

        /// <summary>拖动后锁定当前位置，避免下一步自动摆放覆盖用户选择。</summary>
        private void HandlePositionChanged(Vector2 position)
        {
            _userPosition = true;
        }
    }
}
