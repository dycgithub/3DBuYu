using UnityEngine;

namespace Supply
{
    /// <summary>将抽中奖励逐件放入 GameScene 的普通背包。</summary>
    internal sealed class SupplyBackpackStorage : ISupplyDrawStorage
    {
        private static readonly Dir[] Directions =
        {
            Dir.Down,
            Dir.Left,
            Dir.Up,
            Dir.Right
        };

        private readonly GridView _grid;

        public bool IsReady => _grid != null && _grid.GridType == GridType.StorageForPlay;

        public SupplyBackpackStorage(GridView grid)
        {
            _grid = grid;
        }

        /// <summary>遍历四个方向和全部格位；没有合法位置时丢弃本件奖励。</summary>
        /// <param name="definition">抽中的物品定义。</param>
        /// <returns>物品已生成到背包时返回 true。</returns>
        public bool TryStore(ItemDefinition definition)
        {
            if (!IsReady || definition == null)
            {
                return false;
            }

            _grid.EnsureGridVM();
            ItemVM item = new(definition, _grid.ShapeSet);
            foreach (Dir direction in Directions)
            {
                item.SetDirection(direction);
                for (int row = 0; row < _grid.GridVM.Height; row++)
                {
                    for (int column = 0; column < _grid.GridVM.Width; column++)
                    {
                        if (_grid.CanPlace(item, column, row))
                        {
                            return _grid.SpawnItem(item, column, row) != null;
                        }
                    }
                }
            }

            return false;
        }
    }
}
