using UnityEngine;

namespace SphereMovement
{
    /// <summary>
    /// 球面移动Gizmos绘制器
    /// 负责在Scene视图绘制物体所在位置的局部经纬线和球心
    /// </summary>
    [RequireComponent(typeof(SphereMovement))]
    public class SphereMovementGizmos : MonoBehaviour
    {
        [Header("经纬线设置")]
        [Tooltip("显示的纬度线范围（+/- 度数）")]
        [Range(10f, 90f)]
        public float latitudeRange = 45f;

        [Tooltip("显示的经度线范围（+/- 度数）")]
        [Range(10f, 180f)]
        public float longitudeRange = 60f;

        [Tooltip("线条细分度")]
        [Range(8, 64)]
        public int segments = 24;

        [Tooltip("经纬线颜色")]
        public Color gridColor = new Color(0f, 1f, 1f, 0.6f);

        [Tooltip("物体所在经纬线颜色")]
        public Color currentLineColor = new Color(1f, 1f, 0f, 0.9f);

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

            Vector3 center = _movement.GetSphereCenter();
            float radius = _movement.GetSphereRadius();
            Vector3 normalizedPos = _movement.GetCurrentNormalizedPosition();
            Vector2 sphericalCoords = _movement.GetCurrentSphericalCoords();

            // 绘制球心
            DrawSphereCenter(center);

            // 绘制局部纬线（水平圈）
            DrawLocalLatitudeLines(center, radius, sphericalCoords.y);

            // 绘制局部经线（垂直圈）
            DrawLocalLongitudeLines(center, radius, sphericalCoords.x);

            // 绘制物体当前位置的纬线（高亮）
            DrawCurrentLatitudeLine(center, radius, sphericalCoords.y);

            // 绘制物体当前位置的经线（高亮）
            DrawCurrentLongitudeLine(center, radius, sphericalCoords.x);

            // 绘制物体位置
            DrawCurrentPosition(center + normalizedPos * radius);
        }

        private void DrawSphereCenter(Vector3 center)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, 0.15f);
        }

        private void DrawCurrentPosition(Vector3 position)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(position, 0.1f);
        }

        private void DrawLocalLatitudeLines(Vector3 center, float radius, float currentLatitude)
        {
            Gizmos.color = gridColor;

            float latRangeRad = latitudeRange * Mathf.Deg2Rad;
            float startLat = Mathf.Max(-Mathf.PI / 2f, currentLatitude - latRangeRad);
            float endLat = Mathf.Min(Mathf.PI / 2f, currentLatitude + latRangeRad);

            // 绘制范围内的纬线
            int lineCount = Mathf.CeilToInt(latitudeRange / 15f);
            for (int i = 0; i <= lineCount; i++)
            {
                float t = (float)i / lineCount;
                float lat = Mathf.Lerp(startLat, endLat, t);
                DrawLatitudeLine(center, radius, lat, currentLineColor, segments, false);
            }
        }

        private void DrawCurrentLatitudeLine(Vector3 center, float radius, float latitude)
        {
            DrawLatitudeLine(center, radius, latitude, currentLineColor, segments, true);
        }

        private void DrawLatitudeLine(Vector3 center, float radius, float latitude, Color color, int segs, bool highlight)
        {
            Gizmos.color = color;
            Vector3 prevPoint = Vector3.zero;
            bool firstPoint = true;

            // 绘制完整的纬线圆
            for (int i = 0; i <= segs; i++)
            {
                float lon = Mathf.Lerp(-Mathf.PI, Mathf.PI, (float)i / segs);
                Vector3 localPos = SphericalCoordinates.ToCartesian(new Vector2(lon, latitude)) * radius;
                Vector3 worldPos = center + localPos;

                if (!firstPoint)
                {
                    Gizmos.DrawLine(prevPoint, worldPos);
                }

                prevPoint = worldPos;
                firstPoint = false;
            }

            // 如果是高亮线，画一个小球在起点表示方向
            if (highlight)
            {
                float startLon = -Mathf.PI;
                Vector3 startPos = center + SphericalCoordinates.ToCartesian(new Vector2(startLon, latitude)) * radius;
                Gizmos.DrawWireSphere(startPos, radius * 0.03f);
            }
        }

        private void DrawLocalLongitudeLines(Vector3 center, float radius, float currentLongitude)
        {
            Gizmos.color = gridColor;

            float lonRangeRad = longitudeRange * Mathf.Deg2Rad;
            float startLon = currentLongitude - lonRangeRad;
            float endLon = currentLongitude + lonRangeRad;

            // 处理跨越 -180/180 度的情况
            if (startLon < -Mathf.PI) startLon += 2 * Mathf.PI;
            if (endLon > Mathf.PI) endLon -= 2 * Mathf.PI;

            // 绘制范围内的经线
            int lineCount = Mathf.CeilToInt(longitudeRange / 30f);
            for (int i = 0; i <= lineCount; i++)
            {
                float t = (float)i / lineCount;
                float lon = Mathf.Lerp(startLon, endLon, t);
                if (lon > Mathf.PI) lon -= 2 * Mathf.PI;
                DrawLongitudeLine(center, radius, lon, currentLineColor, segments, false);
            }
        }

        private void DrawCurrentLongitudeLine(Vector3 center, float radius, float longitude)
        {
            DrawLongitudeLine(center, radius, longitude, currentLineColor, segments, true);
        }

        private void DrawLongitudeLine(Vector3 center, float radius, float longitude, Color color, int segs, bool highlight)
        {
            Gizmos.color = color;
            Vector3 prevPoint = Vector3.zero;
            bool firstPoint = true;

            // 绘制完整的经线圆（从北极到南极）
            for (int i = 0; i <= segs; i++)
            {
                float lat = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, (float)i / segs);
                Vector3 localPos = SphericalCoordinates.ToCartesian(new Vector2(longitude, lat)) * radius;
                Vector3 worldPos = center + localPos;

                if (!firstPoint)
                {
                    Gizmos.DrawLine(prevPoint, worldPos);
                }

                prevPoint = worldPos;
                firstPoint = false;
            }

            // 如果是高亮线，画一个小球在起点（北极）表示方向
            if (highlight)
            {
                Vector3 northPole = center + SphericalCoordinates.ToCartesian(new Vector2(longitude, Mathf.PI / 2f)) * radius;
                Gizmos.DrawWireSphere(northPole, radius * 0.03f);
            }
        }
    }
}
