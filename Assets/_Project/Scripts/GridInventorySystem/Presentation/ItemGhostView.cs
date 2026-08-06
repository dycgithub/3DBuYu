using System.Collections.Generic;
using InventorySystem;
using InventorySystem.GridSnap;
using ItemSystem;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.Inventory
{
    /// <summary>
    /// 拖拽幽灵:吸附到网格、随物品旋转重建、按合法性着色。
    /// 单 Image 表达物品整体(尺寸 = 旋转后包围盒),图标来自 ItemVisualRegistry。
    /// 挂在根 Canvas 下,blocksRaycasts 关闭以保证事件穿透到格子的 OnDrop。
    /// </summary>
    public class ItemGhostView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private Color _validColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] private Color _invalidColor = new Color(1f, 0.3f, 0.3f, 0.7f);

        private RectTransform _rect;
        public RectTransform Rect => _rect ??= GetComponent<RectTransform>();

        private void Awake()
        {
            if (TryGetComponent<CanvasGroup>(out var group) == false)
                group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>按物品与旋转重建尺寸(旋转后格数不变,仅位置变化)。</summary>
        public void Show(ItemConfig config, int rotation, float cellSize, float spacing)
        {
            if (_image != null && config != null)
                _image.sprite = ItemVisualHelper.GetIcon(config.itemId);

            var cells = InventoryGrid.GetRotatedCells(config.shape, rotation);
            var (maxC, maxR) = GetBounds(cells);
            Rect.sizeDelta = GridLayoutMath.ShapeSize(maxR + 1, maxC + 1, cellSize, spacing);
        }

        public void SetValid(bool valid)
            => _image.color = valid ? _validColor : _invalidColor;

        /// <summary>幽灵吸附到锚定格:容器本地坐标 → 根 Canvas 本地坐标。</summary>
        public void SnapTo(InventoryGridView gridView, int row, int col)
        {
            if (gridView == null || gridView.RootCanvas == null) return;

            var container = gridView.Container;
            Vector2 localPos = GridLayoutMath.CellLocalPos(row, col, gridView.Step);
            Vector2 world = container.TransformPoint(localPos);

            var rootRect = (RectTransform)gridView.RootCanvas.transform;
            Rect.localPosition = rootRect.InverseTransformPoint(world);
        }

        private static (int x, int y) GetBounds(List<(int row, int col)> cells)
        {
            int minR = int.MaxValue, maxR = int.MinValue;
            int minC = int.MaxValue, maxC = int.MinValue;
            foreach (var (r, c) in cells)
            {
                minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r);
                minC = Mathf.Min(minC, c); maxC = Mathf.Max(maxC, c);
            }
            return (maxC - minC, maxR - minR);
        }
    }

    /// <summary>
    /// 表现查询辅助:从全局容器解析 ItemVisualRegistry。
    /// </summary>
    public static class ItemVisualHelper
    {
        public static Sprite GetIcon(string itemId)
        {
            var scope = ProjectLifetimeScope.Instance;
            if (scope?.Container == null) return null;
            var registry = scope.Container.Resolve<ItemVisualRegistry>();
            return registry.Get(itemId)?.icon;
        }
    }
}
