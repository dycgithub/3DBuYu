using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 物品数据(VM,纯逻辑,不依赖 MonoBehaviour):
/// 形状 = 坐标点集;方向 = Dir 四方向;位置 = 网格锚点坐标(包围盒左上角)。
/// 旋转只改数据,UI 由 View 层驱动刷新。
public class ItemVM
{
    private static int _nextInstanceId;

    /// <summary>物品定义(null = 无定义形状,如测试窗口生成的临时物品)。</summary>
    public ItemDefinition Definition { get; }
    /// <summary>跨 UI 场景和 Game 场景保持不变的本局实例身份。</summary>
    public int InstanceId { get; }

    /// <summary>基础方向(0°)形状点集:x=列偏移, y=行偏移(向右/向下为正)。</summary>
    public IReadOnlyList<Vector2Int> BasePoints{ get; set; }
    /// <summary>当前方向(0°=Down / 90°=Left / 180°=Up / 270°=Right)。</summary>
    public Dir Direction { get; private set; } = Dir.Down;
    /// <summary>锚点(形状左上角)在网格中的坐标:x=列, y=行。</summary>
    public Vector2Int LocalGridCoordinate { get; set; }
    /// <summary>当前方向旋转后的点集(可能有负坐标,配合 RotationOffset 使用)。</summary>
    public IReadOnlyList<Vector2Int> CoordinateSet { get; private set; }
    /// <summary>当前方向的包围盒尺寸(列数, 行数)。</summary>
    public int Width { get; private set; }
    public int Height { get; private set; }
    /// <summary>旋转后锚点修正偏移:使旋转后的形状仍对齐网格左上角。</summary>
    public Vector2Int RotationOffset { get; private set; }

    /// <summary>
    /// 主构造:由物品定义派生形状(形状解析走 shapeSet,默认全局库)。
    /// basePoints 用于跨场景恢复时保留源网格的精确形状,传入后优先于 shapeSet。
    /// </summary>
    public ItemVM(
        ItemDefinition definition,
        ItemShapeSet shapeSet = null,
        Vector2Int origin = default,
        IReadOnlyList<Vector2Int> basePoints = null,
        int instanceId = 0)
    {
        Definition = definition;
        InstanceId = AllocateInstanceId(instanceId);
        var points = basePoints ?? (definition != null
            ? (shapeSet ?? ItemShapeSet.Default).GetPoints(definition.Shape)
            : new List<Vector2Int>());
        BasePoints = new List<Vector2Int>(points ?? new List<Vector2Int>());
        LocalGridCoordinate = origin;
        ApplyDirection(Dir.Down);
    }

    /// <summary>便捷构造:直接给形状点集(测试/无定义场景,Definition 为 null)。</summary>
    public ItemVM(IEnumerable<Vector2Int> basePoints, Vector2Int origin = default, int instanceId = 0)
    {
        Definition = null;
        InstanceId = AllocateInstanceId(instanceId);
        BasePoints = new List<Vector2Int>(basePoints ?? new List<Vector2Int>());
        LocalGridCoordinate = origin;
        ApplyDirection(Dir.Down);
    }
    /// <summary>顺时针旋转 90°。</summary>
    public void Rotate() => ApplyDirection(GridUtilities.RotationHelper.GetNextDir(Direction));

    public void SetDirection(Dir dir) => ApplyDirection(dir);

    void ApplyDirection(Dir dir)
    {
        Direction = dir;
        var rotated=GridUtilities.RotationHelper.RotatePoints(BasePoints,dir);
        CoordinateSet=rotated;
        (Width, Height) = GridUtilities.RotationHelper.GetBoundaryBox(rotated);
        RotationOffset=GridUtilities.RotationHelper.GetRotationOffset(dir,Width,Height);
    }

    private static int AllocateInstanceId(int requestedId)
    {
        if (requestedId > 0)
        {
            int current = Volatile.Read(ref _nextInstanceId);
            while (current < requestedId)
            {
                int previous = Interlocked.CompareExchange(ref _nextInstanceId, requestedId, current);
                if (previous == current)
                    break;
                current = previous;
            }
            return requestedId;
        }

        return Interlocked.Increment(ref _nextInstanceId);
    }
}
