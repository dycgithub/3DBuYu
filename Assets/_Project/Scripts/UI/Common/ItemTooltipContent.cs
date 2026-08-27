using System.Collections.Generic;
using UnityEngine;

namespace _Project.UI.Common
{
    /// <summary>
    /// Item Tooltip 的纯展示数据。
    /// 它只保存已经格式化的文本和当前形状快照，UI 不直接读取战斗配置资产。
    /// </summary>
    public sealed class ItemTooltipContent
    {
        /// <summary>物品名称。</summary>
        public string Name { get; }

        /// <summary>物品的策划描述；为空时由视图显示默认占位文本。</summary>
        public string Description { get; }

        /// <summary>结构化效果文本，多行展示。</summary>
        public string Effects { get; }

        /// <summary>战斗作用范围文本。</summary>
        public string Scope { get; }

        /// <summary>当前方向下的网格占用说明。</summary>
        public string Footprint { get; }

        /// <summary>商店价格文本。</summary>
        public string Price { get; }

        /// <summary>当前 Item 是否处于 Shop Grid。</summary>
        public bool HasPrice { get; }

        /// <summary>物品图标。</summary>
        public Sprite Icon { get; }

        /// <summary>形状预览使用的颜色。</summary>
        public Color Color { get; }

        /// <summary>当前方向下的形状点集。</summary>
        public IReadOnlyList<Vector2Int> CoordinateSet { get; }

        /// <summary>当前方向下用于左上角对齐的偏移。</summary>
        public Vector2Int RotationOffset { get; }

        /// <summary>当前方向下的包围盒列数。</summary>
        public int Width { get; }

        /// <summary>当前方向下的包围盒行数。</summary>
        public int Height { get; }

        public ItemTooltipContent(
            string name,
            string description,
            string effects,
            string scope,
            string footprint,
            string price,
            bool hasPrice,
            Sprite icon,
            Color color,
            IReadOnlyList<Vector2Int> coordinateSet,
            Vector2Int rotationOffset,
            int width,
            int height)
        {
            Name = name;
            Description = description;
            Effects = effects;
            Scope = scope;
            Footprint = footprint;
            Price = price;
            HasPrice = hasPrice;
            Icon = icon;
            Color = color;
            CoordinateSet = coordinateSet;
            RotationOffset = rotationOffset;
            Width = width;
            Height = height;
        }
    }
}
