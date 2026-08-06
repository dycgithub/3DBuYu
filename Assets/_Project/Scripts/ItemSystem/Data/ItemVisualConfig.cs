using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 物品表现配置(ScriptableObject):由 UI 层消费,与逻辑数据(ItemConfig)分离。
    /// 图片与网格 UI 预制体属于表现,由本配置管理;逻辑层不引用。
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory/Item Visual Config")]
    public class ItemVisualConfig : ScriptableObject
    {
        /// <summary>关联逻辑配置的 itemId。</summary>
        public string itemId;

        /// <summary>格子/幽灵显示的图标。</summary>
        public Sprite icon;

        /// <summary>物品在网格中的 UI 预制体(ItemSlotView 挂载),可为空则用通用模板。</summary>
        public GameObject uiPrefab;
    }
}
