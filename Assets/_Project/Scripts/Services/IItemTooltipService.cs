using UnityEngine;

namespace Services
{
    /// <summary>
    /// Item Tooltip 的场景级交互服务。
    /// UIScene 使用固定面板，GameScene 使用跟随鼠标的简洁面板；具体展示由场景注册的实现决定。
    /// </summary>
    public interface IItemTooltipService
    {
        /// <summary>显示或刷新鼠标悬浮物品的简洁 Tooltip。</summary>
        /// <param name="item">鼠标当前悬浮的物品。</param>
        /// <param name="screenPosition">当前指针的屏幕坐标。</param>
        void ShowHover(ItemView item, Vector2 screenPosition);

        /// <summary>更新鼠标悬浮 Tooltip 的位置。</summary>
        /// <param name="item">鼠标当前悬浮的物品。</param>
        /// <param name="screenPosition">当前指针的屏幕坐标。</param>
        void MoveHover(ItemView item, Vector2 screenPosition);

        /// <summary>鼠标离开物品时隐藏悬浮 Tooltip。</summary>
        /// <param name="item">刚刚离开的物品。</param>
        void HideHover(ItemView item);

        /// <summary>刷新当前选中物品的固定 Tooltip 内容。</summary>
        void RefreshSelectedTooltip();

        /// <summary>设置悬浮 Tooltip 的显示开关。</summary>
        void SetHoverTooltipEnabled(bool enabled);

        /// <summary>设置固定 Tooltip 的显示开关。</summary>
        void SetSelectedTooltipEnabled(bool enabled);
    }
}
