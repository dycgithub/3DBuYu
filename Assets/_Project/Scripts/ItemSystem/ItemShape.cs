using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 物品形状 ScriptableObject。
    /// 使用字符串矩阵定义物品在网格中占据的格子形状。
    /// "X" = 占据，"." = 空。
    /// 示例 2x3 形状：
    ///   "XXX"
    ///   "XXX"
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory/Item Shape")]
    public class ItemShape : ScriptableObject
    {
        [Header("形状定义")]
        [Tooltip("每行一个字符串。'X' = 占据。'.' 或 'O' = 空。所有行的长度应相同。")]
        public string[] shapeMatrix = new string[]
        {
            "X"
        };

        /// <summary>形状宽度（格数）。</summary>
        public int Width => shapeMatrix.Length > 0 ? shapeMatrix[0].Length : 0;

        /// <summary>形状高度（格数）。</summary>
        public int Height => shapeMatrix.Length;

        /// <summary>缓存的所有占据格子的 (行偏移, 列偏移) 列表。</summary>
        [System.NonSerialized]
        private List<(int row, int col)> occupiedCells;

        /// <summary>
        /// 获取所有占据格子的 (行偏移, 列偏移) 列表。
        /// 在首次访问时根据 shapeMatrix 计算并缓存。
        /// </summary>
        public List<(int row, int col)> GetOccupiedCells()
        {
            if (occupiedCells == null)
            {
                occupiedCells = new List<(int, int)>();
                for (int r = 0; r < shapeMatrix.Length; r++)
                {
                    string row = shapeMatrix[r];
                    for (int c = 0; c < row.Length; c++)
                    {
                        char ch = row[c];
                        if (ch == 'X' || ch == 'x')
                        {
                            occupiedCells.Add((r, c));
                        }
                    }
                }
            }
            return occupiedCells;
        }

        /// <summary>
        /// 获取旋转后的形状（顺时针90度旋转）。
        /// 用于拖拽放置时的旋转功能。
        /// </summary>
        public List<(int row, int col)> GetRotatedCells()
        {
            int w = Width;
            int h = Height;
            var cells = GetOccupiedCells();
            var rotated = new List<(int, int)>();

            foreach (var (r, c) in cells)
            {
                // 顺时针90度旋转：(r, c) → (c, w - 1 - r)
                rotated.Add((c, w - 1 - r));
            }
            return rotated;
        }

        private void OnEnable()
        {
            // 编辑器/运行时加载时重新计算
            occupiedCells = null;
        }
    }
}
