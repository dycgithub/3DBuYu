using DG.Tweening;
using UnityEngine;

/// <summary>
/// 物品指针按压表现：按下时缩小下沉，抬起时放大上浮并回到视觉初始状态。
/// 只操作 VisualRoot，不改变 ItemView 的网格定位节点。
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemPressEffect : MonoBehaviour
{
    private const float MinimumDuration = 0.01f;
    private const float MinimumScale = 0.5f;

    [Header("引用")]
    [SerializeField, Tooltip("承载物品方块和轮廓的视觉节点；不会改变物品的网格命中区域。")]
    private RectTransform _visualRoot;

    [Header("按下")]
    [SerializeField, Range(0.5f, 1f), Tooltip("按下时相对于初始缩放的倍率。")]
    private float _pressedScale = 0.96f;
    [SerializeField, Min(0f), Tooltip("按下时向下移动的 UI 单位数。")]
    private float _pressedDepth = 3f;
    [SerializeField, Min(0f), Tooltip("按下动画时长，单位为秒。")]
    private float _pressedDuration = 0.08f;
    [SerializeField, Tooltip("按下动画使用的缓动方式。")]
    private Ease _pressedEase = Ease.OutQuad;

    [Header("抬起")]
    [SerializeField, Min(1f), Tooltip("抬起时相对于初始缩放的放大倍率。")]
    private float _releasedScale = 1.04f;
    [SerializeField, Min(0f), Tooltip("抬起时向上移动的 UI 单位数。")]
    private float _releasedLift = 4f;
    [SerializeField, Min(0f), Tooltip("抬起后放大上浮动画时长，单位为秒。")]
    private float _releaseDuration = 0.1f;
    [SerializeField, Min(0f), Tooltip("抬起后回正动画时长，单位为秒。")]
    private float _returnDuration = 0.16f;
    [SerializeField, Tooltip("放大上浮动画使用的缓动方式。")]
    private Ease _releaseEase = Ease.OutQuad;
    [SerializeField, Tooltip("回正动画使用的缓动方式。")]
    private Ease _returnEase = Ease.OutBack;

    private Vector3 _baseScale;
    private Vector2 _basePosition;
    private Tween _visualTween;
    private bool _isPressed;
    private bool _initialized;

    /// <summary>当前是否处于按下状态。</summary>
    public bool IsPressed => _isPressed && isActiveAndEnabled;

    /// <summary>获取承载物品显示的视觉节点。</summary>
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
    }

    private void OnDisable()
    {
        if (!_initialized)
        {
            _isPressed = false;
            return;
        }

        ResetVisualState();
    }

    /// <summary>播放按下时的缩小下沉表现。</summary>
    public void Press()
    {
        EnsureInitialized();
        if (!CanAnimate())
        {
            return;
        }

        _isPressed = true;
        StopTween();
        RestoreVisualState();

        float duration = Mathf.Max(MinimumDuration, _pressedDuration);
        Vector3 pressedScale = _baseScale * Mathf.Clamp(_pressedScale, MinimumScale, 1f);
        Vector2 pressedPosition = _basePosition + Vector2.down * Mathf.Max(0f, _pressedDepth);
        _visualTween = ConfigureUpdate(DOTween.Sequence()
            .Join(_visualRoot.DOScale(pressedScale, duration).SetEase(_pressedEase))
            .Join(_visualRoot.DOAnchorPos(pressedPosition, duration).SetEase(_pressedEase)));
    }

    /// <summary>播放抬起时的放大上浮和回正表现。</summary>
    public void Release()
    {
        EnsureInitialized();
        if (!_isPressed)
        {
            return;
        }

        _isPressed = false;
        StopTween();
        if (!CanAnimate())
        {
            RestoreVisualState();
            return;
        }

        float releaseDuration = Mathf.Max(MinimumDuration, _releaseDuration);
        float returnDuration = Mathf.Max(MinimumDuration, _returnDuration);
        Vector3 releasedScale = _baseScale * Mathf.Max(1f, _releasedScale);
        Vector2 releasedPosition = _basePosition + Vector2.up * Mathf.Max(0f, _releasedLift);
        _visualTween = ConfigureUpdate(DOTween.Sequence()
            .Append(_visualRoot.DOScale(releasedScale, releaseDuration).SetEase(_releaseEase))
            .Join(_visualRoot.DOAnchorPos(releasedPosition, releaseDuration).SetEase(_releaseEase))
            .Append(_visualRoot.DOScale(_baseScale, returnDuration).SetEase(_returnEase))
            .Join(_visualRoot.DOAnchorPos(_basePosition, returnDuration).SetEase(_returnEase))
            .OnComplete(RestoreVisualState));
    }

    /// <summary>取消当前按压表现并立即恢复视觉初始状态。</summary>
    public void Cancel()
    {
        EnsureInitialized();
        _isPressed = false;
        StopTween();
        RestoreVisualState();
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        if (_visualRoot == null)
        {
            _visualRoot = transform.Find("VisualRoot") as RectTransform;
        }

        if (_visualRoot == null)
        {
            Debug.LogWarning("[ItemPressEffect] 未找到 VisualRoot，按压动效不会播放。", this);
        }
        else
        {
            _baseScale = _visualRoot.localScale;
            _basePosition = _visualRoot.anchoredPosition;
        }

        _initialized = true;
    }

    private bool CanAnimate()
    {
        return isActiveAndEnabled && _visualRoot != null;
    }

    private Tween ConfigureUpdate(Tween tween)
    {
        if (Application.isPlaying)
        {
            return tween.SetUpdate(true);
        }

        return tween.SetUpdate(UpdateType.Manual);
    }

    private void StopTween()
    {
        _visualTween?.Kill();
        _visualTween = null;
    }

    private void ResetVisualState()
    {
        _isPressed = false;
        StopTween();
        RestoreVisualState();
    }

    private void RestoreVisualState()
    {
        if (_visualRoot == null)
        {
            return;
        }

        _visualRoot.localScale = _baseScale;
        _visualRoot.anchoredPosition = _basePosition;
    }
}
