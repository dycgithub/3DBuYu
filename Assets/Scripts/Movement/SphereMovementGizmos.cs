using UnityEngine;

namespace SphereMovement
{
    /// <summary>
    /// 球面移动Gizmos绘制器
    /// 负责在Scene视图绘制经纬线和球心
    /// </summary>
    [RequireComponent(typeof(SphereMovement))]
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

        [Tooltip("是否在编辑器中绘制")]
        public bool showGizmos = true;

        private SphereMovement _movement;

        private void Awake()
        {
            _movement = GetComponent<SphereMovement>();
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos || _movement == null) return;

            DrawLatitudeLines();
            DrawLongitudeLines();
            DrawSphereCenter();
        }

        private void DrawSphereCenter()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_movement.GetSphereCenter(), 0.2f);
        }

        private void DrawLatitudeLines()
        {
            Gizmos.color = gridColor;
            float radius = _movement.GetSphereRadius();
            Vector3 center = _movement.GetSphereCenter();

            for (int i = 0; i <= latitudeLines; i++)
            {
                float lat = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, (float)i / latitudeLines);
                DrawLatitudeLine(lat, radius, center);
            }
        }

        private void DrawLatitudeLine(float latitude, float radius, Vector3 center)
        {
            Vector3 prevPoint = Vector3.zero;
            bool firstPoint = true;

            int segments = longitudeLines * 4;

            for (int i = 0; i <= segments; i++)
            {
                float lon = Mathf.Lerp(-Mathf.PI, Mathf.PI, (float)i / segments);
                Vector3 localPos = SphericalCoordinates.ToCartesian(new Vector2(lon, latitude));
                Vector3 worldPos = center + localPos * radius;

                if (!firstPoint)
                {
                    Gizmos.DrawLine(prevPoint, worldPos);
                }

                prevPoint = worldPos;
                firstPoint = false;
            }
        }

        private void DrawLongitudeLines()
        {
            Gizmos.color = gridColor;
            float radius = _movement.GetSphereRadius();
            Vector3 center = _movement.GetSphereCenter();

            for (int i = 0; i <= longitudeLines; i++)
            {
                float lon = Mathf.Lerp(-Mathf.PI, Mathf.PI, (float)i / longitudeLines);
                DrawLongitudeLine(lon, radius, center);
            }
        }

        private void DrawLongitudeLine(float longitude, float radius, Vector3 center)
        {
            Vector3 prevPoint = Vector3.zero;
            bool firstPoint = true;

            int segments = latitudeLines * 4;

            for (int i = 0; i <= segments; i++)
            {
                float lat = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, (float)i / segments);
                Vector3 localPos = SphericalCoordinates.ToCartesian(new Vector2(longitude, lat));
                Vector3 worldPos = center + localPos * radius;

                if (!firstPoint)
                {
                    Gizmos.DrawLine(prevPoint, worldPos);
                }

                prevPoint = worldPos;
                firstPoint = false;
            }
        }
    }
}
