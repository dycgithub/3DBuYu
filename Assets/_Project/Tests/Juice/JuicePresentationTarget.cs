using DG.Tweening;
using Services;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace _Project.Tests.Juice
{
    /// <summary>
    /// CombatLab 中用于验证局部 Juice 的独立表现目标。
    /// <para>该组件由 CombatLabLifetimeScope 的自动注入列表注入，不进入正式战斗组合。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class JuicePresentationTarget : MonoBehaviour
    {
        private const int PunchVibrato = 8;
        private const float PunchElasticity = 0.75f;
        private const float FlashHalf = 0.5f;

        [Header("Juice 目标")]
        [SerializeField, Tooltip("承载 Juice 缩放和颜色反馈的预置视觉节点。")]
        private Transform _visualRoot;
        [SerializeField, Range(0f, 1f), Tooltip("scale punch 滑块的初始值。")]
        private float _scalePunch = 0.35f;
        [SerializeField, Min(0f), Tooltip("scale punch 的最大强度，仅影响本测试场景。")]
        private float _maximumPunch = 0.8f;
        [SerializeField, Min(0.01f), Tooltip("scale punch 的持续时间，单位为秒。")]
        private float _punchDuration = 0.24f;

        [Header("颜色反馈")]
        [SerializeField, Tooltip("命中反馈颜色，仅覆盖测试目标的 Renderer。")]
        private Color _flashColor = new(1f, 0.38f, 0.16f, 1f);
        [SerializeField, Min(0f), Tooltip("颜色反馈的完整持续时间，单位为秒。")]
        private float _flashDuration = 0.16f;

        [Header("测试 UI")]
        [SerializeField, Tooltip("包含静态 Juice 调参控件的 UIDocument。")]
        private UIDocument _uiDocument;

        private ICombatPhaseService _combatPhase;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _colorBlock;
        private Transform _cachedVisualRoot;
        private Vector3 _initialLocalScale;
        private Tween _punchTween;
        private Tween _flashTween;
        private Slider _scaleSlider;
        private Slider _durationSlider;
        private Button _playButton;
        private Label _scaleLabel;
        private Label _durationLabel;
        private Label _statusLabel;
        private bool _controlsBound;

        /// <summary>当前归一化参数映射出的 scale punch 强度。</summary>
        public float CurrentPunch =>
            JuiceTuning.MapScalePunch(_scalePunch, _maximumPunch);

        /// <summary>当前有效的 scale punch 时长。</summary>
        public float PunchDuration => Mathf.Max(0.01f, _punchDuration);

        /// <summary>目标当前是否仍有 Juice 动画在播放。</summary>
        public bool IsPlaying =>
            (_punchTween != null && _punchTween.IsActive()) ||
            (_flashTween != null && _flashTween.IsActive());

        /// <summary>
        /// 接收 CombatLab 阶段服务，使测试目标通过真实 Scope 完成依赖注入。
        /// </summary>
        /// <param name="combatPhase">CombatLab 提供的战斗阶段服务。</param>
        [Inject]
        private void Construct(ICombatPhaseService combatPhase)
        {
            _combatPhase = combatPhase;
            BindControls();
        }

        /// <summary>
        /// 缓存预置视觉节点并在 UIDocument 完成启用后绑定控件。
        /// </summary>
        private void OnEnable()
        {
            CacheVisuals();
            CancelInvoke(nameof(BindControls));

            if (Application.isPlaying)
            {
                Invoke(nameof(BindControls), 0f);
            }
            else
            {
                BindControls();
            }
        }

        /// <summary>
        /// 停止测试动画并解除静态视觉树上的事件监听。
        /// </summary>
        private void OnDisable()
        {
            CancelInvoke(nameof(BindControls));
            StopJuice();
            UnbindControls();
        }

        /// <summary>
        /// 播放一次本地 scale punch 和颜色闪烁反馈。
        /// </summary>
        public void PlayJuice()
        {
            if (_combatPhase != null && !_combatPhase.CanPerformCombatActions)
            {
                return;
            }

            Transform visualRoot = _visualRoot != null ? _visualRoot : transform;
            _punchTween?.Kill();
            RestoreInitialScale();
            _punchTween = visualRoot.DOPunchScale(
                    Vector3.one * CurrentPunch,
                    PunchDuration,
                    PunchVibrato,
                    PunchElasticity)
                .SetUpdate(true)
                .OnComplete(RestoreInitialScale)
                .OnKill(RestoreInitialScale);

            PlayFlash();
            UpdateStatus("PLAYING");
        }

        /// <summary>
        /// 缓存测试场景预先放置的 Renderer，不创建任何运行时表现对象。
        /// </summary>
        private void CacheVisuals()
        {
            _visualRoot ??= transform;
            if (_cachedVisualRoot != _visualRoot)
            {
                _cachedVisualRoot = _visualRoot;
                _initialLocalScale = _visualRoot.localScale;
            }

            _renderers = _visualRoot.GetComponentsInChildren<Renderer>(true);
            _colorBlock ??= new MaterialPropertyBlock();
        }

        /// <summary>
        /// 停止当前目标创建的 Tween，并恢复目标的初始缩放和颜色。
        /// </summary>
        private void StopJuice()
        {
            _punchTween?.Kill();
            _flashTween?.Kill();
            _punchTween = null;
            _flashTween = null;
            RestoreInitialScale();
            ClearFlash();
        }

        /// <summary>
        /// 将视觉节点恢复到第一次缓存的局部缩放。
        /// </summary>
        private void RestoreInitialScale()
        {
            if (_cachedVisualRoot != null)
            {
                _cachedVisualRoot.localScale = _initialLocalScale;
            }
        }

        /// <summary>
        /// 播放从闪烁颜色回到材质原色的短时颜色反馈。
        /// </summary>
        private void PlayFlash()
        {
            if (_renderers == null || _renderers.Length == 0 || _flashDuration <= 0f)
            {
                return;
            }

            _flashTween?.Kill();
            float halfDuration = _flashDuration * FlashHalf;
            _flashTween = DOTween.Sequence()
                .Append(DOVirtual.Float(0f, 1f, halfDuration, ApplyFlash))
                .Append(DOVirtual.Float(1f, 0f, halfDuration, ApplyFlash))
                .SetUpdate(true);
        }

        /// <summary>
        /// 按闪烁进度写入 Renderer 的材质属性块。
        /// </summary>
        /// <param name="progress">从 0 到 1 的闪烁进度。</param>
        private void ApplyFlash(float progress)
        {
            if (progress <= 0f)
            {
                ClearFlash();
                return;
            }

            Color color = Color.Lerp(Color.white, _flashColor, Mathf.Clamp01(progress));
            for (int index = 0; index < _renderers.Length; index++)
            {
                Renderer renderer = _renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_colorBlock);
                _colorBlock.SetColor(BaseColorId, color);
                _colorBlock.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_colorBlock);
            }
        }

        /// <summary>
        /// 清除测试目标写入的颜色属性块。
        /// </summary>
        private void ClearFlash()
        {
            if (_renderers == null)
            {
                return;
            }

            for (int index = 0; index < _renderers.Length; index++)
            {
                if (_renderers[index] != null)
                {
                    _renderers[index].SetPropertyBlock(null);
                }
            }
        }

        /// <summary>
        /// 将 UXML 中已有的 Slider、Button 和 Label 绑定到本地调参字段。
        /// </summary>
        private void BindControls()
        {
            if (_controlsBound)
            {
                return;
            }

            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }

            if (_uiDocument == null || _uiDocument.rootVisualElement == null)
            {
                return;
            }

            VisualElement root = _uiDocument.rootVisualElement;
            _scaleSlider = root.Q<Slider>("scale-punch");
            _durationSlider = root.Q<Slider>("punch-duration");
            _playButton = root.Q<Button>("play-button");
            _scaleLabel = root.Q<Label>("scale-value");
            _durationLabel = root.Q<Label>("duration-value");
            _statusLabel = root.Q<Label>("status-label");

            if (_scaleSlider != null)
            {
                _scaleSlider.SetValueWithoutNotify(Mathf.Clamp01(_scalePunch));
                _scaleSlider.RegisterValueChangedCallback(HandleScaleChanged);
            }

            if (_durationSlider != null)
            {
                _durationSlider.SetValueWithoutNotify(PunchDuration);
                _durationSlider.RegisterValueChangedCallback(HandleDurationChanged);
            }

            if (_playButton != null)
            {
                _playButton.clicked += PlayJuice;
            }

            _controlsBound = true;
            RefreshLabels();
            UpdateStatus("READY");
        }

        /// <summary>
        /// 移除控件回调，防止视觉树销毁后继续调用场景目标。
        /// </summary>
        private void UnbindControls()
        {
            if (!_controlsBound)
            {
                return;
            }

            _scaleSlider?.UnregisterValueChangedCallback(HandleScaleChanged);
            _durationSlider?.UnregisterValueChangedCallback(HandleDurationChanged);
            if (_playButton != null)
            {
                _playButton.clicked -= PlayJuice;
            }

            _scaleSlider = null;
            _durationSlider = null;
            _playButton = null;
            _scaleLabel = null;
            _durationLabel = null;
            _statusLabel = null;
            _controlsBound = false;
        }

        /// <summary>
        /// 将 scale punch 滑块值写回测试场景本地字段。
        /// </summary>
        /// <param name="change">滑块值变化事件。</param>
        private void HandleScaleChanged(ChangeEvent<float> change)
        {
            _scalePunch = Mathf.Clamp01(change.newValue);
            RefreshLabels();
        }

        /// <summary>
        /// 将持续时间滑块值写回测试场景本地字段。
        /// </summary>
        /// <param name="change">滑块值变化事件。</param>
        private void HandleDurationChanged(ChangeEvent<float> change)
        {
            _punchDuration = Mathf.Max(0.01f, change.newValue);
            RefreshLabels();
        }

        /// <summary>
        /// 刷新调参数值 Label，不改变正式游戏状态。
        /// </summary>
        private void RefreshLabels()
        {
            if (_scaleLabel != null)
            {
                _scaleLabel.text = $"{CurrentPunch:0.00}";
            }

            if (_durationLabel != null)
            {
                _durationLabel.text = $"{PunchDuration:0.00}s";
            }
        }

        /// <summary>
        /// 更新静态测试面板的当前状态文字。
        /// </summary>
        /// <param name="status">要显示的状态。</param>
        private void UpdateStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = status;
            }
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
    }
}
