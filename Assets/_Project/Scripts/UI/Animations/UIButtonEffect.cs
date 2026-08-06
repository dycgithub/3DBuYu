using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _Project.UI.Animations
{
    /// <summary>
    /// 按钮动效:出现时由小变大 + 鼠标进入/离开缩放。
    /// 挂在任意带 Button/Image 的 UI 物体上。
    /// </summary>
    public class UIButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Hover")]
        [SerializeField] private float _hoverScale = 1.08f;
        [SerializeField] private float _hoverDuration = 0.15f;

        [Header("Appear")]
        [SerializeField] private bool _appearAnimation = true;
        [SerializeField] private float _appearFrom = 0.85f;
        [SerializeField] private float _appearDuration = 0.25f;
        [SerializeField] private Ease _appearEase = Ease.OutBack;

        private RectTransform _rect;
        private Tweener _hoverTween;
        private Tween _appearTween;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null)
                _rect = transform as RectTransform;
        }

        private void OnEnable()
        {
            if (!_appearAnimation || _rect == null) return;

            _rect.localScale = Vector3.one * _appearFrom;
            _appearTween?.Kill();
            _appearTween = _rect.DOScale(Vector3.one, _appearDuration)
                .SetEase(_appearEase)
                .SetUpdate(true);
        }

        private void OnDisable()
        {
            _hoverTween?.Kill();
            _appearTween?.Kill();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_rect == null) return;
            _hoverTween?.Kill();
            _hoverTween = _rect.DOScale(Vector3.one * _hoverScale, _hoverDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_rect == null) return;
            _hoverTween?.Kill();
            _hoverTween = _rect.DOScale(Vector3.one, _hoverDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
    }
}
