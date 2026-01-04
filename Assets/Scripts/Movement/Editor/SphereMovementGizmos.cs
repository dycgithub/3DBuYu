using UnityEngine;

namespace SphereMovement
{
    /// <summary>
    /// 球面移动调试绘制器
    /// 负责在编辑器中绘制球面网格和中心点
    /// </summary>
    [DisallowMultipleComponent]
    public class SphereMovementGizmos : MonoBehaviour
    {
        [Header("经纬线设置")]
        [Tooltip("纬度线条数")]
        [Range(2, 20)]
        public int latitudeLines = 8;

        [Tooltip("经度线条数")]
        [Range(4, 32)]
        public int longitudeLines = 16;

        [Tooltip("经纬线颜色")]
        public Color gridColor = new Color(0f, 1f, 1f, 0.5f);

        private SphereMovement _movement;

        private void Awake()
        {
            _movement = GetComponent<SphereMovement>();
        }

        private void OnDrawGizmos()
        {
            if (_movement == null) return;

            DrawSphereCenter();
            DrawGridLines();
        }

        private void DrawSphereCenter()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_movement.GetSphereCenter(), 0.2f);
        }

        private void DrawGridLines()
        {
            SphericalCoordinates.DrawSphereGrid(
                _movement.GetSphereCenter(),
                _movement.GetSphereRadius(),
                latitudeLines,
                longitudeLines,
                gridColor
            );
        }
    }
}
