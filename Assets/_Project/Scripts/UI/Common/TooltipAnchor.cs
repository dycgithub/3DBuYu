using UnityEngine;

namespace _Project.UI.Common
{
    public class TooltipAnchor : MonoBehaviour
    {
        [SerializeField] private RectTransform _tooltipRect;
        [SerializeField] private Vector2 _padding = new Vector2(10f, 10f);
        [SerializeField] private float _screenEdgeMargin = 20f;

        private RectTransform _canvasRect;

        private void Start()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _canvasRect = canvas.GetComponent<RectTransform>();
        }

        public void Position(Vector2 screenPosition)
        {
            if (_tooltipRect == null || _canvasRect == null) return;

            bool placeBelow = screenPosition.y > _canvasRect.rect.height * 0.5f;
            bool placeRight = screenPosition.x < _canvasRect.rect.width * 0.5f;

            float x = placeRight
                ? screenPosition.x + _padding.x
                : screenPosition.x - _tooltipRect.rect.width - _padding.x;

            float y = placeBelow
                ? screenPosition.y - _tooltipRect.rect.height - _padding.y
                : screenPosition.y + _padding.y;

            x = Mathf.Clamp(x, _screenEdgeMargin, _canvasRect.rect.width - _tooltipRect.rect.width - _screenEdgeMargin);
            y = Mathf.Clamp(y, _screenEdgeMargin, _canvasRect.rect.height - _tooltipRect.rect.height - _screenEdgeMargin);

            _tooltipRect.position = new Vector2(x, y);
        }
    }
}
