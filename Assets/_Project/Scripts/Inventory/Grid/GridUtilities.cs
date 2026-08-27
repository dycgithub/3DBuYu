using System.Collections.Generic;
using UnityEngine;

public static class GridUtilities
{
    private static Sprite _whiteSprite;

    public static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            }
            return _whiteSprite;
        }
    }
    
    public static class RotationHelper
    {
        //顺时针
        public static Dir GetNextDir(Dir dir) => dir switch
        {

            Dir.Down => Dir.Left,
            Dir.Left => Dir.Up,
            Dir.Up => Dir.Right,
            Dir.Right => Dir.Down,
            _ => Dir.Down,
        };
        public static int GetRotationAngle(Dir dir) => dir switch{
            Dir.Down => 0,
            Dir.Left => 90,
            Dir.Up => 180,
            Dir.Right => 270,
            _ => 0
        };
        
        /// <summary>是否发生了 90°/270° 旋转(宽高会互换)。</summary>
        public static bool IsRotated(Dir dir)=>dir==Dir.Left || dir==Dir.Right;

        /// <summary>顺时针旋转 90°:(x, y) → (-y, x)。</summary>
        public static List<Vector2Int> RotatePointsClockwise(List<Vector2Int> points)
        {
            var result = new List<Vector2Int>(points.Count);
            foreach (var p in points)
            {
                result.Add(new Vector2Int(-p.y, p.x));
            }
            return result;
        }
        /// <summary>按方向旋转点集(0°/90°/180°/270°)。旋转后可能有负坐标,由 RotationOffset 修正。</summary>
        public static List<Vector2Int> RotatePoints(IReadOnlyList<Vector2Int> points, Dir dir)
        {
            var result = new List <Vector2Int>(points.Count);
            foreach (var p in points)
            {
                result.Add(dir switch
                {
                    Dir.Left => new Vector2Int(-p.y, p.x), // 90°
                    Dir.Up => new Vector2Int(-p.x, -p.y), // 180°
                    Dir.Right => new Vector2Int(p.y, -p.x), // 270°
                    _ => new Vector2Int(p.x, p.y) // 0°
                });
            }

            return result;
        }

        public static Vector2Int GetRotationOffset(Dir dir, int width, int height) => dir switch
        {
            Dir.Left => new Vector2Int(width - 1, 0),
            Dir.Up => new Vector2Int(width - 1, height - 1),
            Dir.Right => new Vector2Int(0, height - 1),
            _ => Vector2Int.zero
        };

        public static (int width, int height) GetBoundaryBox(IReadOnlyList<Vector2Int> points)
        {
            if (points == null || points.Count == 0) return (0, 0);
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in points)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            return (maxX - minX + 1, maxY - minY + 1);
        }
        
        /// <summary>常用形状预设(点集约定:x=列偏移, y=行偏移,向右/向下为正)。</summary>
        
    }
    public static class ShapeFactory
    {
        /// <summary>按形状枚举解析点集(Step 2 迁入 ItemShapeSet 后此方法废弃)。</summary>
        public static List<Vector2Int> FromEnum(ItemShape shape) => shape switch
        {
            ItemShape.Vertical2 => Vertical2(),
            ItemShape.Horizontal2 => Horizontal2(),
            ItemShape.Square2x2 => Square2x2(),
            ItemShape.LShape1 => LShape1(),
            ItemShape.LShape2 => LShape2(),
            ItemShape.LShape3 => LShape3(),
            ItemShape.TShape1 => TShape1(),
            ItemShape.TShape2 => TShape2(),
            _ => Single(),
        };

        public static List<Vector2Int> Single() => new() { new Vector2Int(0, 0) };
        public static List<Vector2Int> Vertical2() => new() { new Vector2Int(0, 0), new Vector2Int(0, 1) };
        public static List<Vector2Int> Horizontal2() => new() { new Vector2Int(0, 0), new Vector2Int(1, 0) };
        public static List<Vector2Int> Square2x2() => new() { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };
        public static List<Vector2Int> LShape1() => new() { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };
        public static List<Vector2Int> LShape2() => new() { new Vector2Int(0, 0), new Vector2Int(0, 1) ,new Vector2Int(0,2),new Vector2Int(1,2)};
        public static List<Vector2Int> LShape3() => new() { new Vector2Int(0, 0), new Vector2Int(1, 0) ,new Vector2Int(2,0),new Vector2Int(2,1),new Vector2Int(2,2)};
        public static List<Vector2Int> TShape1() => new() { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) };
        public static List<Vector2Int> TShape2() => new() { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) ,new Vector2Int(1,2) };
    }
}