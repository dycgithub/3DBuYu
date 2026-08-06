using DG.Tweening;
using UnityEngine;

namespace _Project.UI.Animations
{
    /// <summary>
    /// 面板显隐动效。
    /// Scale:由小变大弹入(商店/设置面板)。
    /// Slide:从屏幕外滑入滑出(背包)。
    /// </summary>
    public class UIPanelShowHide : MonoBehaviour
    {
        public enum Mode
        {
            Scale,
            SlideLeft,
            SlideRight
        }

        [SerializeField] private Mode _mode = Mode.Scale;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private float _duration = 0.3f;
        [SerializeField] private Ease _showEase = Ease.OutBack;
        [SerializeField] private Ease _hideEase = Ease.InBack;
        [SerializeField] private bool _startHidden = true;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _homePosition;
        private Vector2 _hiddenPosition;
        private bool _isVisible;
        private Tween _activeTween;

        public bool IsVisible => _isVisible;

        private void Awake()
        {
            _rect = _panel != null ? _panel : GetComponent<RectTransform>();
            _group = GetComponent<CanvasGroup>();
            if (_group == null && _mode == Mode.Scale)
                _group = gameObject.AddComponent<CanvasGroup>();
            _homePosition = _rect.anchoredPosition;

            if (_mode == Mode.Scale)
            {
                _hiddenPosition = _homePosition + new Vector2(0f, -40f);
            }
            else
            {
                ComputeHiddenPosition();
            }

            if (_startHidden)
                SetInstantHidden();
        }

        private void Start()
        {
            if (_mode != Mode.Scale && !_startHidden)
                Show();
        }

        private void ComputeHiddenPosition()
        {
            float width = _rect.rect.width > 0 ? _rect.rect.width : _rect.sizeDelta.x;
            const float margin = 40f;
            float anchorX = _rect.anchorMin.x;
            float pivotX = _rect.pivot.x;

            // 隐藏位必须把面板完整推出父容器(屏幕)之外,否则右锚点面板会露在屏幕中间。
            // 用父容器宽度 + 锚点/轴心算出真正屏幕外(左/右)的位置:
            //   hiddenLeft  = 面板右边缘 <= 父容器左边缘
            //   hiddenRight = 面板左边缘 >= 父容器右边缘
            float parentW = (_rect.parent as RectTransform)?.rect.width ?? 0f;
            if (parentW <= 0f)
            {
                // 兜底:父容器拿不到宽度时,按锚点方向估算
                float sign = anchorX >= 0.5f ? 1f : -1f;
                _hiddenPosition = _homePosition + new Vector2(sign * (width + margin), 0f);
            }
            else
            {
                float hiddenLeft = -(1f - pivotX) * width - anchorX * parentW - margin;
                float hiddenRight = (1f - anchorX) * parentW + pivotX * width + margin;
                float hiddenX = _mode == Mode.SlideLeft ? hiddenLeft : hiddenRight;
                _hiddenPosition = new Vector2(hiddenX, _homePosition.y);
            }
        }

        /// <summary>
        /// 显示位 = 编辑器里放置的位置(即 Awake 捕获的 _homePosition)。
        /// </summary>
        public Vector2 HomePosition => _homePosition;

        private void SetInstantHidden()
        {
            if (_mode == Mode.Scale)
            {
                _rect.localScale = Vector3.one * 0.85f;
                if (_group != null) _group.alpha = 0f;
            }
            else
            {
                _rect.anchoredPosition = _hiddenPosition;
            }
            _isVisible = false;
        }

        public void Show()
        {
            _activeTween?.Kill();
            if (_group != null) _group.blocksRaycasts = true;

            PlayShowTween();
            _isVisible = true;
        }

        private void PlayShowTween()
        {
            if (_mode == Mode.Scale)
            {
                _rect.localScale = Vector3.one * 0.85f;
                if (_group != null) _group.alpha = 0f;

                _activeTween = DOTween.Sequence()
                    .Append(_rect.DOScale(Vector3.one, _duration).SetEase(_showEase))
                    .Join(_group != null ? _group.DOFade(1f, _duration * 0.8f) : DOTween.Sequence())
                    .SetUpdate(true);
            }
            else
            {
                _rect.anchoredPosition = _hiddenPosition;
                _activeTween = _rect.DOAnchorPos(_homePosition, _duration)
                    .SetEase(_showEase)
                    .SetUpdate(true);
            }
        }

        public void Hide()
        {
            _activeTween?.Kill();

            if (_mode == Mode.Scale)
            {
                _activeTween = DOTween.Sequence()
                    .Append(_rect.DOScale(Vector3.one * 0.85f, _duration * 0.7f).SetEase(_hideEase))
                    .Join(_group != null ? _group.DOFade(0f, _duration * 0.6f) : DOTween.Sequence())
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        _isVisible = false;
                        if (_group != null) _group.blocksRaycasts = false;
                    });
            }
            else
            {
                _activeTween = _rect.DOAnchorPos(_hiddenPosition, _duration)
                    .SetEase(_hideEase)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        _isVisible = false;
                        if (_group != null) _group.blocksRaycasts = false;
                    });
            }
        }

        public void Toggle()
        {
            if (_isVisible) Hide();
            else Show();
        }
    }
}
