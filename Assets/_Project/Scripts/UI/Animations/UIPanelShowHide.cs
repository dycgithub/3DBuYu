using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace _Project.UI.Animations
{
    /// <summary>
    /// 场景面板的显隐动效。
    /// 面板主体、遮罩和错峰子项都必须由场景或预制体预先提供，脚本只改变已有对象的状态。
    /// </summary>
    public sealed class UIPanelShowHide : MonoBehaviour
    {
        public enum Mode
        {
            Scale,
            SlideLeft,
            SlideRight
        }

        private sealed class ChildState
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector3 Scale;
        }

        [Header("公共配置")]
        [SerializeField, Tooltip("面板主体 RectTransform；为空时使用当前 GameObject 的 RectTransform。")]
        private RectTransform _panel;
        [SerializeField, Tooltip("控制面板交互状态，并可用于主体透明度；为空时只跳过该 CanvasGroup 操作。")]
        private CanvasGroup _group;
        [SerializeField, Tooltip("初始化时是否立即将面板设置为隐藏状态。")]
        private bool _startHidden = true;

        [Header("主体变换")]
        [SerializeField, Tooltip("是否启用主体的位移、缩放和显示/隐藏变换动画；关闭时改为立即切换状态。")]
        private bool _transformAnimation = true;
        [SerializeField, Tooltip("主体的隐藏方式：缩放、从左侧滑出或从右侧滑出。")]
        private Mode _mode = Mode.Scale;
        [SerializeField, Min(0f), Tooltip("主体显示或隐藏的动画时长，单位为秒。")]
        private float _duration = 0.3f;
        [SerializeField, Tooltip("主体显示时使用的缓动方式。")]
        private Ease _showEase = Ease.OutBack;
        [SerializeField, Tooltip("主体隐藏时使用的缓动方式。")]
        private Ease _hideEase = Ease.InBack;
        [SerializeField, Min(0f), Tooltip("Scale 模式隐藏时向下偏移的 UI 单位数。")]
        private float _scaleOffset = 40f;
        [SerializeField, Range(0.1f, 1f), Tooltip("Scale 模式隐藏时相对于基础缩放的倍率。")]
        private float _hiddenScale = 0.85f;

        [Header("透明度")]
        [SerializeField, Tooltip("是否启用面板和背景遮罩的透明度动画；关闭时立即切换 alpha。")]
        private bool _fadeAnimation = true;
        [SerializeField, Tooltip("背景遮罩使用的 CanvasGroup；为空时只处理主体 CanvasGroup。")]
        private CanvasGroup _backdropGroup;

        [Header("直接子项错峰")]
        [SerializeField, Tooltip("是否启用直接子项的错峰淡入和缩放；关闭时不修改子项状态。")]
        private bool _animateChildren = true;
        [SerializeField, Tooltip("需要执行错峰动画的直接子项根节点；为空时使用面板主体。")]
        private RectTransform _staggerRoot;
        [SerializeField, Min(0f), Tooltip("相邻子项动画的启动间隔，单位为秒。")]
        private float _childStagger = 0.05f;
        [SerializeField, Min(0f), Tooltip("单个子项动画时长，单位为秒。")]
        private float _childDuration = 0.18f;
        [SerializeField, Range(0.1f, 1f), Tooltip("子项隐藏时相对于自身基础缩放的倍率。")]
        private float _childHiddenScale = 0.96f;

        private RectTransform _rect;
        private Vector2 _homePosition;
        private Vector2 _hiddenPosition;
        private Vector3 _homeScale;
        private Vector3 _hiddenScaleVector;
        private readonly List<ChildState> _children = new();
        private Tween _activeTween;
        private bool _isVisible;
        private bool _initialized;

        public bool IsVisible
        {
            get
            {
                EnsureInitialized();
                return _isVisible;
            }
        }

        public Vector2 HomePosition
        {
            get
            {
                EnsureInitialized();
                return _homePosition;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDisable()
        {
            _activeTween?.Kill();
            _activeTween = null;
            _isVisible = false;
            SetInteraction(false);
        }

        private void ResolveReferences()
        {
            _rect = GetComponent<RectTransform>();
            _panel ??= _rect;
            _group ??= GetComponent<CanvasGroup>();
            _staggerRoot ??= _panel;
        }

        private void CaptureBaseState()
        {
            if (_panel == null)
                return;

            _homePosition = _panel.anchoredPosition;
            _homeScale = _panel.localScale;
            _hiddenScaleVector = _homeScale * Mathf.Max(0.1f, _hiddenScale);

            if (_mode == Mode.Scale)
            {
                _hiddenPosition = _homePosition + Vector2.down * _scaleOffset;
                return;
            }

            ComputeHiddenPosition();
        }

        private void CacheChildStates()
        {
            _children.Clear();
            if (!_animateChildren || _staggerRoot == null)
                return;

            for (int i = 0; i < _staggerRoot.childCount; i++)
            {
                Transform child = _staggerRoot.GetChild(i);
                RectTransform rect = child as RectTransform;
                if (rect == null)
                    continue;

                _children.Add(new ChildState
                {
                    Rect = rect,
                    Group = rect.GetComponent<CanvasGroup>(),
                    Scale = rect.localScale
                });
            }
        }

        private void ComputeHiddenPosition()
        {
            float width = _panel.rect.width > 0f ? _panel.rect.width : _panel.sizeDelta.x;
            const float margin = 40f;
            float anchorX = _panel.anchorMin.x;
            float pivotX = _panel.pivot.x;
            float parentWidth = (_panel.parent as RectTransform)?.rect.width ?? 0f;

            if (parentWidth <= 0f)
            {
                float direction = _mode == Mode.SlideLeft ? -1f : 1f;
                _hiddenPosition = _homePosition + new Vector2(direction * (width + margin), 0f);
                return;
            }

            float hiddenLeft = -(1f - pivotX) * width - anchorX * parentWidth - margin;
            float hiddenRight = (1f - anchorX) * parentWidth + pivotX * width + margin;
            _hiddenPosition = new Vector2(
                _mode == Mode.SlideLeft ? hiddenLeft : hiddenRight,
                _homePosition.y);
        }

        public void Show()
        {
            EnsureInitialized();
            if (_panel == null)
                return;

            _activeTween?.Kill();
            _isVisible = true;
            SetInteraction(false);
            if (!_transformAnimation && !_fadeAnimation && !_animateChildren)
            {
                SetInstantVisible();
                return;
            }

            SetShowStartState();
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(CreateShowTween());
            InsertChildTweens(sequence, true);
            _activeTween = sequence.OnComplete(() => SetInteraction(true));
        }

        public void Hide()
        {
            EnsureInitialized();
            if (_panel == null)
                return;

            _activeTween?.Kill();
            SetInteraction(false);
            if (!_transformAnimation && !_fadeAnimation && !_animateChildren)
            {
                SetInstantHidden();
                return;
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(CreateHideTween());
            InsertChildTweens(sequence, false);
            _activeTween = sequence.OnComplete(() =>
            {
                _isVisible = false;
                SetInstantHidden();
            });
        }

        public void Toggle()
        {
            EnsureInitialized();
            if (_isVisible)
                Hide();
            else
                Show();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            ResolveReferences();
            CaptureBaseState();
            CacheChildStates();

            if (_startHidden)
                SetInstantHidden();
            else
                SetInstantVisible();

            _initialized = true;
        }

        private Tween CreateShowTween()
        {
            Sequence tween = DOTween.Sequence();
            if (_transformAnimation)
            {
                if (_mode == Mode.Scale)
                {
                    tween.Join(_panel.DOScale(_homeScale, _duration).SetEase(_showEase));
                    tween.Join(_panel.DOAnchorPos(_homePosition, _duration).SetEase(_showEase));
                }
                else
                {
                    tween.Join(_panel.DOAnchorPos(_homePosition, _duration).SetEase(_showEase));
                }
            }

            if (_fadeAnimation)
                AddFadeTween(tween, 1f, _duration * 0.8f);
            return tween;
        }

        private Tween CreateHideTween()
        {
            Sequence tween = DOTween.Sequence();
            if (_transformAnimation)
            {
                if (_mode == Mode.Scale)
                {
                    tween.Join(_panel.DOScale(_hiddenScaleVector, _duration * 0.7f).SetEase(_hideEase));
                    tween.Join(_panel.DOAnchorPos(_hiddenPosition, _duration * 0.7f).SetEase(_hideEase));
                }
                else
                {
                    tween.Join(_panel.DOAnchorPos(_hiddenPosition, _duration).SetEase(_hideEase));
                }
            }
            else
            {
                _panel.localScale = _hiddenScaleVector;
                _panel.anchoredPosition = _hiddenPosition;
            }

            if (_fadeAnimation)
                AddFadeTween(tween, 0f, _duration * 0.6f);
            else
                SetFadeState(0f);
            return tween;
        }

        private void SetFadeState(float alpha)
        {
            if (_group != null)
                _group.alpha = alpha;
            if (_backdropGroup != null && _backdropGroup != _group)
                _backdropGroup.alpha = alpha;
        }

        private void AddFadeTween(Sequence sequence, float targetAlpha, float duration)
        {
            if (_group != null)
                sequence.Join(_group.DOFade(targetAlpha, duration));
            if (_backdropGroup != null && _backdropGroup != _group)
                sequence.Join(_backdropGroup.DOFade(targetAlpha, duration));
        }

        private void InsertChildTweens(Sequence sequence, bool show)
        {
            if (!_animateChildren)
                return;

            for (int i = 0; i < _children.Count; i++)
            {
                ChildState child = _children[show ? i : _children.Count - 1 - i];
                float delay = i * Mathf.Max(0f, _childStagger);
                float duration = Mathf.Max(0f, _childDuration);
                if (child.Group != null)
                {
                    child.Group.alpha = show ? 0f : 1f;
                    sequence.Insert(delay, child.Group.DOFade(show ? 1f : 0f, duration));
                }

                child.Rect.localScale = show
                    ? child.Scale * Mathf.Max(0.1f, _childHiddenScale)
                    : child.Scale;
                sequence.Insert(
                    delay,
                    child.Rect.DOScale(show ? child.Scale : child.Scale * _childHiddenScale, duration)
                        .SetEase(show ? Ease.OutQuad : Ease.InQuad));
            }
        }

        private void SetShowStartState()
        {
            if (_panel != null)
            {
                if (_transformAnimation)
                {
                    _panel.localScale = _hiddenScaleVector;
                    _panel.anchoredPosition = _hiddenPosition;
                }
                else
                {
                    _panel.localScale = _homeScale;
                    _panel.anchoredPosition = _homePosition;
                }
            }

            if (_fadeAnimation)
                SetFadeState(0f);
            else
                SetFadeState(1f);

            if (!_animateChildren)
                return;

            for (int i = 0; i < _children.Count; i++)
            {
                ChildState child = _children[i];
                if (child.Group != null)
                    child.Group.alpha = 0f;
                child.Rect.localScale = child.Scale * Mathf.Max(0.1f, _childHiddenScale);
            }
        }

        private void SetInstantHidden()
        {
            if (_panel != null)
            {
                _panel.localScale = _hiddenScaleVector;
                _panel.anchoredPosition = _hiddenPosition;
            }

            SetFadeState(0f);

            if (_animateChildren)
            {
                for (int i = 0; i < _children.Count; i++)
                {
                    ChildState child = _children[i];
                    if (child.Group != null)
                        child.Group.alpha = 0f;
                    child.Rect.localScale = child.Scale * Mathf.Max(0.1f, _childHiddenScale);
                }
            }

            _isVisible = false;
            SetInteraction(false);
        }

        private void SetInstantVisible()
        {
            if (_panel != null)
            {
                _panel.localScale = _homeScale;
                _panel.anchoredPosition = _homePosition;
            }

            SetFadeState(1f);

            if (_animateChildren)
            {
                for (int i = 0; i < _children.Count; i++)
                {
                    ChildState child = _children[i];
                    if (child.Group != null)
                        child.Group.alpha = 1f;
                    child.Rect.localScale = child.Scale;
                }
            }

            _isVisible = true;
            SetInteraction(true);
        }

        private void SetInteraction(bool enabled)
        {
            if (_group != null)
            {
                _group.interactable = enabled;
                _group.blocksRaycasts = enabled;
            }
        }
    }
}
