using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 形状库(对齐 Cholopol TIS 的 TetrisItemPointSet_SO):
/// 集中定义 ItemShape 枚举 → 点集,多个物品可复用同一形状。
/// 未指定资产时使用 Default(内置标准形状,零配置可用)。
/// </summary>
[CreateAssetMenu(fileName = "ItemShapeSet", menuName = "Inventory/Item Shape Set")]
public class ItemShapeSet : ScriptableObject
{
    [SerializeField] private List<PointSet> shapes = new();

    private static ItemShapeSet _default;

    /// <summary>
    /// 全局默认形状库。惰性初始化:编辑模式首次访问也会构建,
    /// Play 模式由 RuntimeInitializeOnLoadMethod 预建。
    /// </summary>
    public static ItemShapeSet Default
    {
        get
        {
            if (_default == null) BuildDefault();
            return _default;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BuildDefault()
    {
        _default = CreateInstance<ItemShapeSet>();
        _default.shapes = new List<PointSet>
        {
            new PointSet { Shape = ItemShape.Single,      Points = GridUtilities.ShapeFactory.Single() },
            new PointSet { Shape = ItemShape.Vertical2,   Points = GridUtilities.ShapeFactory.Vertical2() },
            new PointSet { Shape = ItemShape.Horizontal2, Points = GridUtilities.ShapeFactory.Horizontal2() },
            new PointSet { Shape = ItemShape.Square2x2,   Points = GridUtilities.ShapeFactory.Square2x2() },
            new PointSet { Shape = ItemShape.LShape1,      Points = GridUtilities.ShapeFactory.LShape1() },
            new PointSet { Shape = ItemShape.LShape2,      Points = GridUtilities.ShapeFactory.LShape2() },
            new PointSet { Shape = ItemShape.LShape3,      Points = GridUtilities.ShapeFactory.LShape3() },
            new PointSet { Shape = ItemShape.TShape1,      Points = GridUtilities.ShapeFactory.TShape1() },
            new PointSet { Shape = ItemShape.TShape2,      Points = GridUtilities.ShapeFactory.TShape2() },
            new PointSet { Shape = ItemShape.Plus3,        Points = GridUtilities.ShapeFactory.Plus3() },
            new PointSet { Shape = ItemShape.Plus5,        Points = GridUtilities.ShapeFactory.Plus5() },
            new PointSet { Shape = ItemShape.StarCluster,  Points = GridUtilities.ShapeFactory.StarCluster() },
        };
    }

    /// <summary>按形状查询点集；自定义库未配置星群时使用标准十七格，未知形状回退单格。</summary>
    public IReadOnlyList<Vector2Int> GetPoints(ItemShape shape)
    {
        if (shapes != null)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                var ps = shapes[i];
                if (ps != null && ps.Shape == shape) return ps.Points;
            }
        }

        return shape == ItemShape.StarCluster
            ? GridUtilities.ShapeFactory.StarCluster()
            : GridUtilities.ShapeFactory.Single();
    }
}
