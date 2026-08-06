using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 物品身份信息。图片已由 UI 层 ItemVisualConfig 负责,此处不再持有 icon。
    /// </summary>
    [System.Serializable]
    public class ItemIdentity
    {
        public string itemId;
        public string displayName;
        [TextArea(2, 5)] public string description;
    }
}
