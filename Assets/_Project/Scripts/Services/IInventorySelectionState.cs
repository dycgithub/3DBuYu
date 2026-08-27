using System;

namespace Services
{
    /// <summary>
    /// 管理当前场景中被选中的网格物品。
    /// 不负责拖拽数据或网格放置判定；物品视图负责把用户输入转发到此服务。
    /// </summary>
    public interface IInventorySelectionState
    {
        /// <summary>当前选中的物品视图；没有选中物品时为 <c>null</c>。</summary>
        ItemView SelectedItem { get; }

        /// <summary>
        /// 选中状态变化事件。参数为新的选中物品；清除选中时参数为 <c>null</c>。
        /// </summary>
        event Action<ItemView> SelectionChanged;

        /// <summary>选中指定物品，并取消之前物品的选中样式。</summary>
        /// <param name="item">要选中的物品视图；传入 <c>null</c> 不执行操作。</param>
        void Select(ItemView item);

        /// <summary>清除当前选中物品。</summary>
        void ClearSelection();
    }
}
