using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI.Common
{
    /// <summary>
    /// Item Tooltip 的 UGUI 展示层。
    /// 面板和内容控件由 prefab 序列化绑定，本类只刷新内容。
    /// </summary>
    public sealed class ItemTooltip : MonoBehaviour
    {
        private const string EmptyText = "暂无效果描述";

        [Header("References")]
        [SerializeField] private RectTransform _rect;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _scopeText;
        [SerializeField] private TextMeshProUGUI _effectsText;
        [SerializeField] private TextMeshProUGUI _footprintText;
        [SerializeField] private TextMeshProUGUI _priceText;
        [SerializeField] private RectTransform _shapeRoot;
        [SerializeField] private Image[] _shapeCells;

        private bool _detailed;
        private bool _initialized;

        /// <summary>当前是否处于显示状态。</summary>
        public bool IsVisible => gameObject.activeSelf;

        /// <summary>
        /// 配置当前面板模式。固定面板显示完整内容，移动面板显示简洁内容。
        /// </summary>
        /// <param name="detailed">是否读取完整信息布局。</param>
        public void Initialize(bool detailed)
        {
            if (_initialized && _detailed == detailed)
                return;

            _detailed = detailed;
            _initialized = true;

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            DisableRaycastsOnChildren();
        }

        /// <summary>显示指定内容并刷新场景中已有的文本、图标和形状格子。</summary>
        /// <param name="content">已经格式化的 Tooltip 内容；为空时隐藏面板。</param>
        public void Show(ItemTooltipContent content)
        {
            if (content == null)
            {
                Hide();
                return;
            }

            if (!_initialized)
                Initialize(_detailed);

            gameObject.SetActive(true);
            if (_nameText != null)
                _nameText.text = string.IsNullOrWhiteSpace(content.Name) ? "未命名物品" : content.Name;

            if (_detailed)
            {
                if (_descriptionText != null)
                    _descriptionText.text = string.IsNullOrWhiteSpace(content.Description)
                        ? EmptyText
                        : content.Description;
                if (_scopeText != null)
                {
                    _scopeText.text = content.Scope;
                    _scopeText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.Scope));
                }
                if (_effectsText != null)
                    _effectsText.text = string.IsNullOrWhiteSpace(content.Effects)
                        ? EmptyText
                        : content.Effects;
                if (_footprintText != null)
                    _footprintText.text = content.Footprint;
                if (_priceText != null)
                {
                    _priceText.text = content.Price;
                    _priceText.gameObject.SetActive(content.HasPrice);
                }
                if (_icon != null)
                {
                    _icon.sprite = content.Icon;
                    _icon.gameObject.SetActive(content.Icon != null);
                }
                RebuildShapePreview(content);
            }
            else if (_descriptionText != null)
            {
                _descriptionText.text = BuildCompactDescription(content);
            }

            if (_rect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
        }

        /// <summary>隐藏面板并复用而不是销毁当前形状格子。</summary>
        public void Hide()
        {
            ClearShapePreview();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 将移动 Tooltip 放置在指针附近，并限制在所属 Canvas 内。
        /// </summary>
        /// <param name="screenPosition">指针屏幕坐标。</param>
        public void PositionNearScreen(Vector2 screenPosition)
        {
            if (_rect == null || !gameObject.activeSelf)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null)
                return;

            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition,
                    camera,
                    out Vector2 localPoint))
                return;

            Rect canvasBounds = canvasRect.rect;
            float width = _rect.rect.width;
            float height = _rect.rect.height;
            const float padding = 12f;

            float left = localPoint.x < canvasBounds.center.x
                ? localPoint.x + padding
                : localPoint.x - width - padding;
            float top = localPoint.y > canvasBounds.center.y
                ? localPoint.y - padding
                : localPoint.y + height + padding;

            left = Mathf.Clamp(left, canvasBounds.xMin, canvasBounds.xMax - width);
            top = Mathf.Clamp(top, canvasBounds.yMin + height, canvasBounds.yMax);

            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.zero;
            _rect.pivot = new Vector2(0f, 1f);
            _rect.anchoredPosition = new Vector2(
                left - canvasBounds.xMin,
                top - canvasBounds.yMin);
        }

        private void DisableRaycastsOnChildren()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        private void RebuildShapePreview(ItemTooltipContent content)
        {
            ClearShapePreview();
            if (_shapeRoot == null || content.CoordinateSet == null || content.CoordinateSet.Count == 0)
                return;

            float cellSize = 24f;
            if (content.Width > 0)
                cellSize = Mathf.Min(cellSize, 180f / content.Width);
            if (content.Height > 0)
                cellSize = Mathf.Min(cellSize, 72f / content.Height);

            float step = cellSize + 3f;
            Color color = content.Color;
            color.a = Mathf.Max(0.75f, color.a);

            if (_shapeCells == null)
                return;

            int count = Mathf.Min(_shapeCells.Length, content.CoordinateSet.Count);
            for (int i = 0; i < count; i++)
            {
                Image cell = _shapeCells[i];
                if (cell == null)
                    continue;

                Vector2Int point = content.CoordinateSet[i] + content.RotationOffset;
                RectTransform cellRect = cell.rectTransform;
                cellRect.anchorMin = new Vector2(0f, 1f);
                cellRect.anchorMax = new Vector2(0f, 1f);
                cellRect.pivot = new Vector2(0f, 1f);
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                cellRect.anchoredPosition = new Vector2(point.x * step, -point.y * step);
                cell.sprite = cell.sprite != null ? cell.sprite : GridUtilities.WhiteSprite;
                cell.color = color;
                cell.raycastTarget = false;
                cell.gameObject.SetActive(true);
            }
        }

        private void ClearShapePreview()
        {
            if (_shapeCells == null)
                return;

            for (int i = 0; i < _shapeCells.Length; i++)
            {
                if (_shapeCells[i] != null)
                    _shapeCells[i].gameObject.SetActive(false);
            }
        }

        private static string BuildCompactDescription(ItemTooltipContent content)
        {
            string description = string.IsNullOrWhiteSpace(content.Description)
                ? string.Empty
                : content.Description.Trim();
            string effects = string.IsNullOrWhiteSpace(content.Effects)
                ? string.Empty
                : content.Effects.Trim();

            if (description.Length == 0 && effects.Length == 0)
                return EmptyText;
            if (description.Length == 0)
                return effects;
            if (effects.Length == 0)
                return description;
            return $"{description}\n{effects}";
        }
    }
}
