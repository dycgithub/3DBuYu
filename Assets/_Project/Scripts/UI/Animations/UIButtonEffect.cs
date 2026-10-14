using DG.Tweening;
using GameSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.Animations
{
    /// <summary>
    /// 为 UGUI 按钮提供进入、退出、按下和可选松开音效，并可选播放预先配置的视觉反馈。
    /// 新增组件默认只播放音效，视觉节点仍由场景或预制体显式配置。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonEffect : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        ISubmitHandler
    {
        private const string DefaultHoverSfx = "ButtonIn";
        private const string DefaultExitSfx = "ButtonOut";
        private const string DefaultPressSfx = "ButtonInOut";

        [Header("引用")]
        [SerializeField, Tooltip("承载缩放、位移和旋转的视觉节点；不会改变按钮的命中区域。")]
        private RectTransform _visualRoot;
        [SerializeField, Tooltip("悬停时执行颜色变化的 Graphic；为空时跳过颜色变化。")]
        private Graphic _colorTarget;
        [SerializeField, Tooltip("点击光圈的 RectTransform；为空时跳过光圈效果。")]
        private RectTransform _ripple;
        [SerializeField, Tooltip("点击光圈使用的 CanvasGroup；为空时只执行缩放，不执行透明度变化。")]
        private CanvasGroup _rippleGroup;

        [Header("出现")]
        [SerializeField, Tooltip("是否启用对象启用时的出现动画。")]
        private bool _appearAnimation;
        [SerializeField, Range(0.1f, 1f), Tooltip("出现动画的初始缩放比例。")]
        private float _appearFrom = 0.85f;
        [SerializeField, Min(0f), Tooltip("出现动画时长，单位为秒。")]
        private float _appearDuration = 0.25f;
        [SerializeField, Tooltip("出现动画使用的缓动方式。")]
        private Ease _appearEase = Ease.OutBack;

        [Header("悬停")]
        [SerializeField, Tooltip("是否启用指针悬停时的缩放、位移、倾斜和颜色变化。")]
        private bool _hoverAnimation;
        [SerializeField, Min(1f), Tooltip("悬停时相对于基础缩放的倍率。")]
        private float _hoverScale = 1.04f;
        [SerializeField, Min(0f), Tooltip("悬停时向上移动的 UI 单位数。")]
        private float _hoverLift = 4f;
        [SerializeField, Min(0f), Tooltip("悬停动画时长，单位为秒。")]
        private float _hoverDuration = 0.15f;
        [SerializeField, Range(0f, 10f), Tooltip("悬停时随机倾斜角度范围，单位为度；0 表示不倾斜。")]
        private float _hoverTilt = 2f;

        [Header("按下")]
        [SerializeField, Tooltip("是否启用指针按下和松开时的缩放、位移动画。")]
        private bool _pressAnimation;
        [SerializeField, Range(0.5f, 1f), Tooltip("按下时 X 轴相对于基础缩放的倍率。")]
        private float _pressedScaleX = 0.98f;
        [SerializeField, Range(0.5f, 1f), Tooltip("按下时 Y 轴相对于基础缩放的倍率。")]
        private float _pressedScaleY = 0.92f;
        [SerializeField, Min(0f), Tooltip("按下时向下移动的 UI 单位数。")]
        private float _pressedOffset = 4f;
        [SerializeField, Min(0f), Tooltip("按下动画时长，单位为秒。")]
        private float _pressedDuration = 0.08f;
        [SerializeField, Min(0f), Tooltip("松开后恢复悬停或待机状态的时长，单位为秒。")]
        private float _releasedDuration = 0.2f;

        [Header("点击光圈")]
        [SerializeField, Tooltip("是否启用点击时的光圈动画。")]
        private bool _rippleAnimation;
        [SerializeField, Min(0f), Tooltip("光圈从出现到结束的时长，单位为秒。")]
        private float _rippleDuration = 0.28f;
        [SerializeField, Min(0f), Tooltip("光圈结束时相对于初始尺寸的缩放倍率。")]
        private float _rippleScale = 1.35f;

        [Header("音效")]
        [SerializeField, Tooltip("是否启用指针进入、退出、按下和可选松开音效。")]
        private bool _soundEnabled = true;
        [SerializeField, Tooltip("指针进入按钮时播放的音效名称；留空时使用 ButtonIn。")]
        private string _hoverSfx = DefaultHoverSfx;
        [SerializeField, Tooltip("指针离开按钮时播放的音效名称；留空时使用 ButtonOut。")]
        private string _exitSfx = DefaultExitSfx;
        [SerializeField, Tooltip("按下按钮时播放的音效名称；留空时使用 ButtonInOut。")]
        private string _pressSfx = DefaultPressSfx;
        [SerializeField, Tooltip("松开按钮时播放的音效名称；留空时不播放。")]
        private string _releaseSfx;
        [SerializeField, Range(0f, 1f), Tooltip("按钮音效音量，范围为 0 到 1。")]
        private float _sfxVolume = 1f;
        [SerializeField, Range(0.5f, 2f), Tooltip("随机音调的最小倍率。")]
        private float _minimumPitch = 0.96f;
        [SerializeField, Range(0.5f, 2f), Tooltip("随机音调的最大倍率。")]
        private float _maximumPitch = 1.04f;

        private Button _button;
        private RectTransform _hitArea;
        private Vector3 _baseScale;
        private Vector2 _basePosition;
        private Quaternion _baseRotation;
        private Color _normalColor;
        private bool _isHovered;
        private bool _isPressed;
        private Tween _visualTween;
        private Tween _colorTween;
        private Tween _appearTween;
        private Tween _rippleTween;
        private bool _initialized;

        [Inject] private AudioManager _audioManager;

        public bool IsPressed => _isPressed && isActiveAndEnabled;
        public RectTransform VisualRoot
        {
            get
            {
                EnsureInitialized();
                return _visualRoot;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            ResetVisualState();

            if (!_appearAnimation || _visualRoot == null)
                return;

            _visualRoot.localScale = _baseScale * Mathf.Max(0.1f, _appearFrom);
            _appearTween?.Kill();
            _appearTween = _visualRoot.DOScale(_baseScale, _appearDuration)
                .SetEase(_appearEase)
                .SetUpdate(true);
        }

        private void OnDisable()
        {
            KillTweens();
            _isHovered = false;
            _isPressed = false;
            ResetVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            EnsureInitialized();
            if (!CanInteract() || _isHovered)
            {
                return;
            }

            _isHovered = true;
            if (_hoverAnimation && !_isPressed)
                AnimateHover();
            PlaySfx(_hoverSfx, DefaultHoverSfx);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            EnsureInitialized();
            bool wasHovered = _isHovered;
            _isHovered = false;

            if (_hoverAnimation && !_isPressed)
                AnimateIdle(_releasedDuration);

            if (wasHovered && CanInteract())
            {
                PlaySfx(_exitSfx, DefaultExitSfx);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureInitialized();
            if (!CanInteract() || _isPressed)
            {
                return;
            }

            _isPressed = true;
            if (_pressAnimation)
            {
                _appearTween?.Kill();
                _appearTween = null;
                KillVisualTweens();
                if (_visualRoot != null)
                {
                    _visualTween = DOTween.Sequence()
                        .Join(_visualRoot.DOScale(Vector3.Scale(
                            _baseScale,
                            new Vector3(_pressedScaleX, _pressedScaleY, 1f)),
                            _pressedDuration).SetEase(Ease.OutQuad))
                        .Join(_visualRoot.DOAnchorPos(
                            _basePosition + Vector2.down * _pressedOffset,
                            _pressedDuration).SetEase(Ease.OutQuad))
                        .Join(_visualRoot.DOLocalRotateQuaternion(_baseRotation, _pressedDuration))
                        .SetUpdate(true);
                }
            }

            PlaySfx(_pressSfx, DefaultPressSfx);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EnsureInitialized();
            if (!_isPressed)
                return;

            _isPressed = false;
            PlaySfx(_releaseSfx);
            if (_hoverAnimation && _isHovered)
                AnimateHover(_releasedDuration);
            else if (_pressAnimation)
                AnimateIdle(_releasedDuration);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            EnsureInitialized();
            if (CanInteract() && _rippleAnimation)
                PlayRipple(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            EnsureInitialized();
            if (CanInteract())
            {
                PlaySfx(_pressSfx, DefaultPressSfx);
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _hitArea = transform as RectTransform;
            _button = GetComponent<Button>();
            ResolveReferences();
            CaptureBaseState();
            _initialized = true;
        }

        private void ResolveReferences()
        {
            _visualRoot ??= transform.Find("VisualRoot") as RectTransform;
            _colorTarget ??= GetComponent<Graphic>();
            _ripple ??= transform.Find("RippleLayer") as RectTransform;

            if (_rippleGroup == null && _ripple != null)
                _rippleGroup = _ripple.GetComponent<CanvasGroup>();
        }

        private void CaptureBaseState()
        {
            if (_visualRoot != null)
            {
                _baseScale = _visualRoot.localScale;
                _basePosition = _visualRoot.anchoredPosition;
                _baseRotation = _visualRoot.localRotation;
            }

            if (_colorTarget != null)
                _normalColor = _colorTarget.color;
        }

        private bool CanInteract()
        {
            return isActiveAndEnabled && (_button == null || _button.interactable);
        }

        private void AnimateHover(float duration = -1f)
        {
            if (!_hoverAnimation || _visualRoot == null)
                return;

            KillVisualTweens();
            float tilt = _hoverTilt > 0f ? Random.Range(-_hoverTilt, _hoverTilt) : 0f;
            float actualDuration = duration >= 0f ? duration : _hoverDuration;
            _visualTween = DOTween.Sequence()
                .Join(_visualRoot.DOScale(_baseScale * _hoverScale, actualDuration).SetEase(Ease.OutQuad))
                .Join(_visualRoot.DOAnchorPos(
                    _basePosition + Vector2.up * _hoverLift,
                    actualDuration).SetEase(Ease.OutQuad))
                .Join(_visualRoot.DOLocalRotate(new Vector3(0f, 0f, tilt), actualDuration)
                    .SetEase(Ease.OutQuad))
                .SetUpdate(true);

            AnimateColor(Color.Lerp(_normalColor, Color.white, 0.16f), actualDuration);
        }

        private void AnimateIdle(float duration)
        {
            if (_visualRoot != null)
            {
                KillVisualTweens();
                _visualTween = DOTween.Sequence()
                    .Join(_visualRoot.DOScale(_baseScale, duration).SetEase(Ease.OutBack))
                    .Join(_visualRoot.DOAnchorPos(_basePosition, duration).SetEase(Ease.OutBack))
                    .Join(_visualRoot.DOLocalRotateQuaternion(_baseRotation, duration)
                        .SetEase(Ease.OutBack))
                    .SetUpdate(true);
            }

            if (_hoverAnimation)
                AnimateColor(_normalColor, duration);
        }

        private void AnimateColor(Color target, float duration)
        {
            if (_colorTarget == null)
                return;

            _colorTween?.Kill();
            _colorTween = _colorTarget.DOColor(target, duration).SetUpdate(true);
        }

        private void PlayRipple(PointerEventData eventData)
        {
            if (!_rippleAnimation || _ripple == null)
                return;

            if (eventData != null && _ripple.parent is RectTransform parent &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                _ripple.anchoredPosition = localPoint;
            }

            _rippleTween?.Kill();
            _ripple.localScale = Vector3.zero;
            if (_rippleGroup != null)
                _rippleGroup.alpha = 1f;

            Sequence rippleSequence = DOTween.Sequence()
                .Append(_ripple.DOScale(Vector3.one * _rippleScale, _rippleDuration)
                    .SetEase(Ease.OutQuad));
            if (_rippleGroup != null)
                rippleSequence.Join(_rippleGroup.DOFade(0f, _rippleDuration));

            _rippleTween = rippleSequence
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    _ripple.localScale = Vector3.zero;
                    if (_rippleGroup != null)
                        _rippleGroup.alpha = 0f;
                });
        }

        /// <summary>优先播放按钮显式配置的音效，缺省时使用该交互的默认切片。</summary>
        private void PlaySfx(string name, string fallbackName = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = fallbackName;
            }

            if (!_soundEnabled || _audioManager == null || string.IsNullOrWhiteSpace(name))
                return;

            float minimum = Mathf.Min(_minimumPitch, _maximumPitch);
            float maximum = Mathf.Max(_minimumPitch, _maximumPitch);
            _audioManager.PlaySFXByName(name, _sfxVolume, Random.Range(minimum, maximum));
        }

        private void KillVisualTweens()
        {
            _visualTween?.Kill();
            _visualTween = null;
        }

        private void KillTweens()
        {
            KillVisualTweens();
            _colorTween?.Kill();
            _colorTween = null;
            _appearTween?.Kill();
            _appearTween = null;
            _rippleTween?.Kill();
            _rippleTween = null;
        }

        private void ResetVisualState()
        {
            if (_visualRoot != null)
            {
                _visualRoot.localScale = _baseScale;
                _visualRoot.anchoredPosition = _basePosition;
                _visualRoot.localRotation = _baseRotation;
            }

            if (_colorTarget != null)
                _colorTarget.color = _normalColor;

            if (_ripple != null)
                _ripple.localScale = Vector3.zero;
            if (_rippleGroup != null)
                _rippleGroup.alpha = 0f;
        }
    }
}
