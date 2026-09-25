using DG.Tweening;
using UnityEngine;
using _Project.UI.Tutorial.Model;

namespace _Project.UI.Tutorial.View
{
    /// <summary>用四块预制遮罩在目标周围形成可点击透明区域。</summary>
    public sealed class TutorialOverlayView : MonoBehaviour
    {
        [Header("坐标空间")]
        [SerializeField] private RectTransform _overlayRoot;

        [Header("遮罩")]
        [SerializeField] private RectTransform _topBlocker;
        [SerializeField] private RectTransform _bottomBlocker;
        [SerializeField] private RectTransform _leftBlocker;
        [SerializeField] private RectTransform _rightBlocker;
        [SerializeField] private RectTransform _focusFrame;
        [SerializeField] private CanvasGroup _group;

        [Header("动画")]
        [SerializeField, Min(0f)] private float _transitionDuration = 0.2f;

        private Tween _transition;

        /// <summary>当前高亮区域在遮罩坐标系中的矩形。</summary>
        public Rect FocusRect { get; private set; }

        /// <summary>当前是否存在有效高亮区域。</summary>
        public bool HasFocus { get; private set; }

        /// <summary>当前遮罩是否已经完成最后一次过渡。</summary>
        public bool IsReady => !_isTransitioning;

        private bool _isTransitioning;

        /// <summary>初始化遮罩坐标空间和默认引用。</summary>
        private void Awake()
        {
            _overlayRoot ??= transform as RectTransform;
            _group ??= GetComponent<CanvasGroup>();
            ConfigureRect(_topBlocker);
            ConfigureRect(_bottomBlocker);
            ConfigureRect(_leftBlocker);
            ConfigureRect(_rightBlocker);
            ConfigureRect(_focusFrame);
        }

        /// <summary>显示指定步骤的遮罩并移动高亮区域。</summary>
        /// <param name="step">当前教程步骤。</param>
        /// <param name="target">当前步骤目标；无目标时使用全屏遮罩。</param>
        public void Show(TutorialStepData step, TutorialAnchor target)
        {
            if (_overlayRoot == null || step == null)
            {
                return;
            }

            HasFocus = step.FocusMode == TutorialFocusMode.Target &&
                       target != null &&
                       target.RectTransform != null;
            FocusRect = HasFocus
                ? CalculateFocusRect(target.RectTransform, step.FocusPadding)
                : _overlayRoot.rect;

            _transition?.Kill();
            _isTransitioning = true;
            SetActiveState(HasFocus ? TutorialFocusMode.Target : TutorialFocusMode.FullScreen);
            if (_group != null)
            {
                bool shouldBlockInput = step.FocusMode == TutorialFocusMode.FullScreen || HasFocus;
                _group.blocksRaycasts = shouldBlockInput;
                _group.interactable = shouldBlockInput;
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(AnimateRect(
                _topBlocker,
                HasFocus ? GetTopRect(FocusRect) : _overlayRoot.rect,
                _transitionDuration));
            sequence.Join(AnimateRect(
                _bottomBlocker,
                HasFocus ? GetBottomRect(FocusRect) : new Rect(),
                _transitionDuration));
            sequence.Join(AnimateRect(
                _leftBlocker,
                HasFocus ? GetLeftRect(FocusRect) : new Rect(),
                _transitionDuration));
            sequence.Join(AnimateRect(
                _rightBlocker,
                HasFocus ? GetRightRect(FocusRect) : new Rect(),
                _transitionDuration));
            sequence.Join(AnimateRect(_focusFrame, HasFocus ? FocusRect : new Rect(), _transitionDuration));
            if (_group != null)
            {
                sequence.Join(_group.DOFade(1f, _transitionDuration));
            }
            _transition = sequence.OnComplete(() => _isTransitioning = false);
        }

        /// <summary>在目标位置变化后立即同步高亮区域。</summary>
        /// <param name="step">当前教程步骤。</param>
        /// <param name="target">当前步骤目标。</param>
        /// <returns>高亮区域发生变化时返回 true。</returns>
        public bool Refresh(TutorialStepData step, TutorialAnchor target)
        {
            if (step == null || step.FocusMode != TutorialFocusMode.Target ||
                target == null || target.RectTransform == null)
            {
                return false;
            }

            Rect focusRect = CalculateFocusRect(target.RectTransform, step.FocusPadding);
            if (HasFocus && FocusRect == focusRect)
            {
                return false;
            }

            _transition?.Kill();
            _transition = null;
            _isTransitioning = false;
            HasFocus = true;
            FocusRect = focusRect;
            SetActiveState(TutorialFocusMode.Target);
            ApplyRect(_topBlocker, GetTopRect(focusRect));
            ApplyRect(_bottomBlocker, GetBottomRect(focusRect));
            ApplyRect(_leftBlocker, GetLeftRect(focusRect));
            ApplyRect(_rightBlocker, GetRightRect(focusRect));
            ApplyRect(_focusFrame, focusRect);
            return true;
        }

        /// <summary>淡出遮罩并停止拦截场景输入。</summary>
        public void Hide()
        {
            _transition?.Kill();
            float duration = Mathf.Max(0f, _transitionDuration);
            if (_group == null || duration <= 0f)
            {
                SetHidden();
                return;
            }

            _transition = _group.DOFade(0f, duration)
                .SetUpdate(true)
                .OnComplete(SetHidden);
        }

        /// <summary>释放动画并恢复遮罩的非交互状态。</summary>
        private void OnDestroy()
        {
            _transition?.Kill();
        }

        /// <summary>计算目标在遮罩坐标系中的包围矩形。</summary>
        private Rect CalculateFocusRect(RectTransform target, Vector2 padding)
        {
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 firstLocal = _overlayRoot.InverseTransformPoint(corners[0]);
            float minimumX = firstLocal.x;
            float maximumX = firstLocal.x;
            float minimumY = firstLocal.y;
            float maximumY = firstLocal.y;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector3 local = _overlayRoot.InverseTransformPoint(corners[index]);
                minimumX = Mathf.Min(minimumX, local.x);
                maximumX = Mathf.Max(maximumX, local.x);
                minimumY = Mathf.Min(minimumY, local.y);
                maximumY = Mathf.Max(maximumY, local.y);
            }

            Rect bounds = _overlayRoot.rect;
            float paddingX = Mathf.Max(0f, padding.x);
            float paddingY = Mathf.Max(0f, padding.y);
            return Rect.MinMaxRect(
                Mathf.Clamp(minimumX - paddingX, bounds.xMin, bounds.xMax),
                Mathf.Clamp(minimumY - paddingY, bounds.yMin, bounds.yMax),
                Mathf.Clamp(maximumX + paddingX, bounds.xMin, bounds.xMax),
                Mathf.Clamp(maximumY + paddingY, bounds.yMin, bounds.yMax));
        }

        /// <summary>将矩形转换为顶部遮罩区域。</summary>
        private Rect GetTopRect(Rect focus)
        {
            Rect bounds = _overlayRoot.rect;
            return Rect.MinMaxRect(bounds.xMin, focus.yMax, bounds.xMax, bounds.yMax);
        }

        /// <summary>将矩形转换为底部遮罩区域。</summary>
        private Rect GetBottomRect(Rect focus)
        {
            Rect bounds = _overlayRoot.rect;
            return Rect.MinMaxRect(bounds.xMin, bounds.yMin, bounds.xMax, focus.yMin);
        }

        /// <summary>将矩形转换为左侧遮罩区域。</summary>
        private Rect GetLeftRect(Rect focus)
        {
            Rect bounds = _overlayRoot.rect;
            return Rect.MinMaxRect(bounds.xMin, focus.yMin, focus.xMin, focus.yMax);
        }

        /// <summary>将矩形转换为右侧遮罩区域。</summary>
        private Rect GetRightRect(Rect focus)
        {
            Rect bounds = _overlayRoot.rect;
            return Rect.MinMaxRect(focus.xMax, focus.yMin, bounds.xMax, focus.yMax);
        }

        /// <summary>为一个遮罩矩形创建位移和尺寸动画。</summary>
        private static Tween AnimateRect(RectTransform target, Rect rect, float duration)
        {
            if (target == null)
            {
                return DOTween.Sequence();
            }

            Vector2 center = rect.center;
            Vector2 size = new(Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
            return DOTween.Sequence()
                .Join(target.DOAnchorPos(center, duration).SetEase(Ease.OutQuad))
                .Join(target.DOSizeDelta(size, duration).SetEase(Ease.OutQuad));
        }

        /// <summary>立即设置遮罩矩形的位置和尺寸。</summary>
        private static void ApplyRect(RectTransform target, Rect rect)
        {
            if (target == null)
            {
                return;
            }
            target.anchoredPosition = rect.center;
            target.sizeDelta = new Vector2(Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
        }

        /// <summary>设置所有遮罩的中心锚点。</summary>
        private static void ConfigureRect(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>切换高亮边框的显示状态。</summary>
        private void SetActiveState(TutorialFocusMode focusMode)
        {
            if (_focusFrame != null)
            {
                _focusFrame.gameObject.SetActive(focusMode == TutorialFocusMode.Target);
            }
        }

        /// <summary>隐藏所有遮罩并关闭其输入拦截。</summary>
        private void SetHidden()
        {
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }
            SetActiveState(TutorialFocusMode.FullScreen);
            HasFocus = false;
            _isTransitioning = false;
        }
    }
}
