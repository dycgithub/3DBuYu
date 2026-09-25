using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _Project.UI.Tutorial.View
{
    /// <summary>通过标题栏拖动教程弹窗，并将位置限制在父级安全区域内。</summary>
    public sealed class PanelDragHandle : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private RectTransform _bounds;

        private Vector2 _pointerOffset;
        private bool _dragging;
        private bool _hasForbiddenRect;
        private Rect _forbiddenRect;

        /// <summary>弹窗位置改变时触发。</summary>
        public event Action<Vector2> PositionChanged;

        /// <summary>初始化默认的弹窗和边界引用。</summary>
        private void Awake()
        {
            _panel ??= transform.parent as RectTransform;
            _bounds ??= _panel?.parent as RectTransform;
        }

        /// <summary>开始拖动时记录鼠标与弹窗锚点的偏移。</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanDrag(eventData))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _bounds,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            _pointerOffset = _panel.anchoredPosition - localPoint;
            _dragging = true;
            _panel.SetAsLastSibling();
        }

        /// <summary>按指针位置移动弹窗，并限制越界和遮挡高亮目标。</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !CanDrag(eventData))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _bounds,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Vector2 desired = ClampPosition(localPoint + _pointerOffset);
            if (!IsAllowedPosition(desired))
            {
                return;
            }

            _panel.anchoredPosition = desired;
            PositionChanged?.Invoke(desired);
        }

        /// <summary>结束当前拖动。</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
        }

        /// <summary>设置当前步骤的高亮区域为弹窗禁入区域。</summary>
        /// <param name="forbiddenRect">与弹窗同一父级坐标系中的高亮区域。</param>
        public void SetForbiddenRect(Rect forbiddenRect)
        {
            _forbiddenRect = forbiddenRect;
            _hasForbiddenRect = true;
        }

        /// <summary>清除弹窗禁入区域。</summary>
        public void ClearForbiddenRect()
        {
            _hasForbiddenRect = false;
        }

        /// <summary>限制弹窗完整矩形位于边界矩形内。</summary>
        private Vector2 ClampPosition(Vector2 position)
        {
            Rect bounds = _bounds.rect;
            Rect panel = _panel.rect;
            float minimumX = bounds.xMin + panel.width * _panel.pivot.x;
            float maximumX = bounds.xMax - panel.width * (1f - _panel.pivot.x);
            float minimumY = bounds.yMin + panel.height * _panel.pivot.y;
            float maximumY = bounds.yMax - panel.height * (1f - _panel.pivot.y);
            return new Vector2(
                Mathf.Clamp(position.x, minimumX, maximumX),
                Mathf.Clamp(position.y, minimumY, maximumY));
        }

        /// <summary>判断弹窗新位置是否不会覆盖当前高亮目标。</summary>
        private bool IsAllowedPosition(Vector2 position)
        {
            if (!_hasForbiddenRect)
            {
                return true;
            }

            Rect panel = _panel.rect;
            Rect positionedPanel = new(
                position.x - panel.width * _panel.pivot.x,
                position.y - panel.height * _panel.pivot.y,
                panel.width,
                panel.height);
            return !positionedPanel.Overlaps(_forbiddenRect);
        }

        /// <summary>检查拖动所需的 RectTransform 和指针数据。</summary>
        private bool CanDrag(PointerEventData eventData)
        {
            return isActiveAndEnabled && eventData != null && _panel != null && _bounds != null;
        }
    }
}
