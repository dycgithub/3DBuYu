using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI.MetaballMenu
{
    /// <summary>
    /// 一个融球入口的配置和局部表现。
    /// 能量扣除、进度推进和面板打开由 MetaballMenuController 统一管理。
    /// </summary>
    public sealed class MetaballMenuItem : MonoBehaviour
    {
        [Header("入口")]
        [SerializeField] private string _title = "入口";
        [SerializeField] private KeyCode _holdKey = KeyCode.F1;
        [SerializeField] private MetaballMenuTarget _target;

        [Header("融球参数")]
        [SerializeField, Range(0f, 360f)] private float _orbitAngle = 90f;
        [SerializeField, Min(0f)] private float _orbitRadius;
        [SerializeField, Min(1f)] private float _ballRadius = 76f;
        [SerializeField, Min(0.1f)] private float _fusionDuration = 0.8f;
        [SerializeField, Min(0f)] private float _energyCostPerSecond = 12f;
        [SerializeField] private Color _ballColor = Color.white;

        [Header("显示")]
        [SerializeField] private RectTransform _visualRoot;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _keyLabel;
        [SerializeField] private Image _progressRing;
        [SerializeField] private CanvasGroup _contentGroup;

        private MetaballFusionProgress _progress;

        public string Title => string.IsNullOrWhiteSpace(_title) ? _target.ToString() : _title;
        public KeyCode HoldKey => _holdKey;
        public MetaballMenuTarget Target => _target;
        public float OrbitAngle => _orbitAngle;
        public float OrbitRadius => _orbitRadius;
        public float BallRadius => Mathf.Max(1f, _ballRadius);
        public float FusionDuration => Mathf.Max(0.1f, _fusionDuration);
        public float EnergyCostPerSecond => Mathf.Max(0f, _energyCostPerSecond);
        public Color BallColor => _ballColor;
        public MetaballFusionProgress Progress
        {
            get
            {
                InitializeRuntime();
                return _progress;
            }
        }

        public RectTransform VisualRoot => _visualRoot != null ? _visualRoot : transform as RectTransform;

        private void Awake()
        {
            ResolveViewReferences();
            InitializeRuntime();
            RefreshStaticView();
        }

        /// <summary>创建与当前 Inspector 配置对应的运行时进度。</summary>
        public void InitializeRuntime()
        {
            if (_progress == null)
                _progress = new MetaballFusionProgress(FusionDuration);
        }

        /// <summary>设置子球在菜单根节点本地坐标中的位置。</summary>
        public void SetLocalPosition(Vector2 position)
        {
            if (VisualRoot != null)
                VisualRoot.anchoredPosition = position;
        }

        /// <summary>刷新标题、按键提示和融合进度圈。</summary>
        public void RefreshView(float progress, bool isHeld, bool blocked)
        {
            if (_progressRing != null)
            {
                _progressRing.fillAmount = Mathf.Clamp01(progress);
                Color ringColor = _ballColor;
                ringColor.a = isHeld ? 1f : 0.7f;
                if (blocked)
                    ringColor.a *= 0.45f;
                _progressRing.color = ringColor;
            }

            if (_contentGroup != null)
                _contentGroup.alpha = blocked ? 0.5f : 1f;
        }

        private void ResolveViewReferences()
        {
            _visualRoot ??= transform as RectTransform;
            _contentGroup ??= GetComponent<CanvasGroup>();

            if (_titleLabel == null)
                _titleLabel = FindText("Title");
            if (_keyLabel == null)
                _keyLabel = FindText("Key");
            if (_progressRing == null)
                _progressRing = FindProgressRing();
        }

        private void RefreshStaticView()
        {
            if (_titleLabel != null)
                _titleLabel.text = Title;
            if (_keyLabel != null)
                _keyLabel.text = FormatKey(_holdKey);
        }

        private TMP_Text FindText(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private Image FindProgressRing()
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject.name == "ProgressRing")
                    return images[i];
            }

            return null;
        }

        private static string FormatKey(KeyCode key)
        {
            if (key == KeyCode.None)
                return "-";

            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return ((int)key - (int)KeyCode.Alpha0).ToString();

            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
                return $"Num{(int)key - (int)KeyCode.Keypad0}";

            return key.ToString();
        }
    }
}
