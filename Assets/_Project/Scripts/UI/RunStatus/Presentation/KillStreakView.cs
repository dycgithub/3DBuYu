using System;
using R3;
using Services;
using TMPro;
using UnityEngine;
using VContainer;

namespace _Project.UI.RunStatus
{
    /// <summary>
    /// 将本局连杀状态显示为由 Hierarchy 配置的 UGUI HUD。
    /// <para>视图只订阅服务状态，不创建或配置运行时 UI，也不直接修改战斗数据。</para>
    /// </summary>
    public sealed class KillStreakView : MonoBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("Hierarchy 中连杀面板的 CanvasGroup。")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [Tooltip("Hierarchy 中显示当前连杀数的 TMP 文本。")]
        [SerializeField] private TextMeshProUGUI _currentLabel;
        [Tooltip("Hierarchy 中显示最高连杀数的 TMP 文本。")]
        [SerializeField] private TextMeshProUGUI _bestLabel;

        [Header("表现")]
        [Tooltip("没有连杀时的目标透明度。")]
        [SerializeField, Range(0f, 1f)] private float _idleAlpha = 0.35f;
        [Tooltip("连杀增加后的目标透明度。")]
        [SerializeField, Range(0f, 1f)] private float _activeAlpha = 1f;
        [Tooltip("透明度每秒接近目标值的速度。")]
        [SerializeField, Min(0f)] private float _fadeSpeed = 6f;
        [Tooltip("连杀增加时面板的初始缩放倍数。")]
        [SerializeField, Min(1f)] private float _punchScale = 1.14f;
        [Tooltip("面板缩放每秒恢复到 1 的速度。")]
        [SerializeField, Min(0f)] private float _scaleRecoverySpeed = 5f;
        [Tooltip("当前连杀文本格式。")]
        [SerializeField] private string _currentFormat = "连杀 ×{0}";
        [Tooltip("最高连杀文本格式。")]
        [SerializeField] private string _bestFormat = "最高 ×{0}";

        [Inject] private IKillStreakService _killStreak;

        private float _targetAlpha;
        private int _lastStreak;
        private IDisposable _currentSubscription;
        private IDisposable _bestSubscription;

        private void Start()
        {
            if (!HasReferences())
            {
                Debug.LogWarning("[KillStreakView] CanvasGroup、当前连杀文本和最高连杀文本都必须绑定。", this);
                return;
            }

            _targetAlpha = Mathf.Clamp01(_idleAlpha);
            _canvasGroup.alpha = _targetAlpha;

            if (_killStreak == null)
            {
                Debug.LogWarning("[KillStreakView] 未注入 IKillStreakService。", this);
                return;
            }

            _currentSubscription = _killStreak.CurrentStreak.Subscribe(HandleCurrentStreakChanged);
            _bestSubscription = _killStreak.BestStreak.Subscribe(HandleBestStreakChanged);
        }

        private void Update()
        {
            if (!HasReferences())
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha,
                _targetAlpha,
                Mathf.Max(0f, _fadeSpeed) * deltaTime);

            float scale = Mathf.MoveTowards(
                _canvasGroup.transform.localScale.x,
                1f,
                Mathf.Max(0f, _scaleRecoverySpeed) * deltaTime);
            _canvasGroup.transform.localScale = Vector3.one * scale;
        }

        private void OnDestroy()
        {
            _currentSubscription?.Dispose();
            _bestSubscription?.Dispose();
        }

        private bool HasReferences()
        {
            return _canvasGroup != null && _currentLabel != null && _bestLabel != null;
        }

        private void HandleCurrentStreakChanged(int streak)
        {
            int normalizedStreak = Mathf.Max(0, streak);
            _currentLabel.text = string.Format(_currentFormat, normalizedStreak);

            if (normalizedStreak > _lastStreak && normalizedStreak > 0)
            {
                _canvasGroup.transform.localScale = Vector3.one * Mathf.Max(1f, _punchScale);
                _targetAlpha = Mathf.Clamp01(_activeAlpha);
            }
            else if (normalizedStreak == 0)
            {
                _targetAlpha = Mathf.Clamp01(_idleAlpha);
            }

            _lastStreak = normalizedStreak;
        }

        private void HandleBestStreakChanged(int streak)
        {
            _bestLabel.text = string.Format(_bestFormat, Mathf.Max(0, streak));
        }
    }
}
