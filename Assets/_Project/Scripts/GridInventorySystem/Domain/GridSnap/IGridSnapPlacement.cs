using System.Collections.Generic;

namespace InventorySystem.GridSnap
{
    /// <summary>
    /// 放置校验抽象:吸附模块不依赖任何库存实现。
    /// 背包通过 InventoryGridPlacement 适配;建造模式/地图编辑器直接实现本接口即可复用。
    /// </summary>
    public interface IGridSnapPlacement
    {
        /// <summary>旋转后的形状 cells 能否以 anchor 为 (0,0) 偏移放置(边界+占用)。</summary>
        bool CanPlaceAt(IReadOnlyList<SnapCell> cells, SnapCell anchor);
    }
}
