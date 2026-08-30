using System;
using R3;
using Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.RunStatus
{
    /// <summary>
    /// 将本局连杀状态显示为独立的 UGUI HUD。
    /// <para>视图只订阅服务状态，不直接推进连杀计时或修改战斗数据。</para>
    /// </summary>
    public sealed class KillStreakView : MonoBehaviour
    {
        [Header("字体")]
        [SerializeField] private TMP_FontAsset _fontAsset;

        [Header("布局")]
        [SerializeField] private Vector2 _panelSize = new(360f, 86f);
        [SerializeField] private Vector2 _panelOffset = new(0f, -24f);
        [SerializeField, Min(0f)] private float _currentFontSize = 30f;
        [SerializeField, Min(0f)] private float _bestFontSize = 16f;

        [Header("表现")]
        [SerializeField, Range(0f, 1f)] private float _idleAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _activeAlpha = 1f;
        [SerializeField, Min(0f)] private float _fadeSpeed = 6f;
        [SerializeField, Min(1f)] private float _punchScale = 1.14f;
        [SerializeField, Min(0f)] private float _scaleRecoverySpeed = 5f;
        [SerializeField] private Color _panelColor = new(0.01f, 0.035f, 0.065f, 0.55f);
        [SerializeField] private Color _currentColor = new(1f, 0.75f, 0.22f, 1f);
        [SerializeField] private Color _bestColor = new(0.75f, 0.85f, 1f, 0.9f);
        [SerializeField] private string _currentFormat = "连杀 ×{0}";
        [SerializeField] private string _bestFormat = "最高 ×{0}";

        [Inject] private IKillStreakService _killStreak;

        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _currentLabel;
        private TextMeshProUGUI _bestLabel;
        private float _targetAlpha;
        private int _lastStreak;
        private IDisposable _currentSubscription;
        private IDisposable _bestSubscription;

        private void Awake()
        {
            CreatePanel();
            _targetAlpha = Mathf.Clamp01(_idleAlpha);
            _canvasGroup.alpha = _targetAlpha;
        }

        private void Start()
        {
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
            if (_canvasGroup == null)
                return;

            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return;

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

        private void CreatePanel()
        {
            var panelObject = new GameObject(
                "KillStreakOverlay",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image));
            panelObject.transform.SetParent(transform, false);

            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 1f);
            panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = _panelOffset;
            panel.sizeDelta = _panelSize;

            _canvasGroup = panelObject.GetComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            Image background = panelObject.GetComponent<Image>();
            background.color = _panelColor;
            background.raycastTarget = false;

            _currentLabel = CreateLabel(
                panel,
                "CurrentStreak",
                new Vector2(0f, -8f),
                new Vector2(_panelSize.x, 46f),
                _currentFontSize,
                _currentColor,
                FontStyles.Bold);
            _bestLabel = CreateLabel(
                panel,
                "BestStreak",
                new Vector2(0f, -57f),
                new Vector2(_panelSize.x, 24f),
                _bestFontSize,
                _bestColor,
                FontStyles.Normal);
        }

        private TextMeshProUGUI CreateLabel(
            RectTransform parent,
            string objectName,
            Vector2 position,
            Vector2 size,
            float fontSize,
            Color color,
            FontStyles fontStyle)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            if (_fontAsset != null)
                label.font = _fontAsset;
            label.fontSize = Mathf.Max(0f, fontSize);
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
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
