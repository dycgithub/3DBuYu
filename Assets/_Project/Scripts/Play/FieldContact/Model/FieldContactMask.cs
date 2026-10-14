using UnityEngine;

namespace Play.FieldContact.Model
{
    /// <summary>计算球面与邻近渲染体之间的显现遮罩参数。</summary>
    public static class FieldContactMask
    {
        private const float MinimumRevealDistance = 0.0001f;

        /// <summary>计算目标最近点与球面之间的非负间距。</summary>
        /// <param name="fieldCenter">球面中心。</param>
        /// <param name="fieldRadius">球面半径。</param>
        /// <param name="closestPoint">目标几何体上的最近点。</param>
        /// <returns>最近点在球面外侧时的间距；相交时返回零。</returns>
        public static float GetSurfaceGap(
            Vector3 fieldCenter,
            float fieldRadius,
            Vector3 closestPoint)
        {
            float centerDistance = Vector3.Distance(fieldCenter, closestPoint);
            return Mathf.Max(0f, centerDistance - Mathf.Max(0f, fieldRadius));
        }

        /// <summary>将目标表面间距映射为平滑显现强度。</summary>
        /// <param name="surfaceGap">目标与球面的间距。</param>
        /// <param name="revealDistance">开始显现的最大间距。</param>
        /// <returns>从接触时的 1 平滑衰减到范围边缘的 0。</returns>
        public static float GetRevealStrength(float surfaceGap, float revealDistance)
        {
            float safeDistance = Mathf.Max(MinimumRevealDistance, revealDistance);
            float safeGap = Mathf.Max(0f, surfaceGap);
            float normalizedGap = Mathf.InverseLerp(0f, safeDistance, safeGap);

            return 1f - Mathf.SmoothStep(0f, 1f, normalizedGap);
        }

        /// <summary>点位落在包围盒内部时，求离点最近的包围盒表面点。</summary>
        /// <param name="bounds">目标渲染体的世界空间包围盒。</param>
        /// <param name="point">待测世界空间点。</param>
        /// <returns>包围盒表面或外部的最近点。</returns>
        public static Vector3 GetClosestBoundsSurfacePoint(Bounds bounds, Vector3 point)
        {
            if (!bounds.Contains(point))
            {
                return bounds.ClosestPoint(point);
            }

            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            float distanceToLeft = point.x - minimum.x;
            float distanceToRight = maximum.x - point.x;
            float distanceToBottom = point.y - minimum.y;
            float distanceToTop = maximum.y - point.y;
            float distanceToBack = point.z - minimum.z;
            float distanceToFront = maximum.z - point.z;

            float closestDistance = distanceToLeft;
            Vector3 closestPoint = new(minimum.x, point.y, point.z);

            if (distanceToRight < closestDistance)
            {
                closestDistance = distanceToRight;
                closestPoint = new Vector3(maximum.x, point.y, point.z);
            }

            if (distanceToBottom < closestDistance)
            {
                closestDistance = distanceToBottom;
                closestPoint = new Vector3(point.x, minimum.y, point.z);
            }

            if (distanceToTop < closestDistance)
            {
                closestDistance = distanceToTop;
                closestPoint = new Vector3(point.x, maximum.y, point.z);
            }

            if (distanceToBack < closestDistance)
            {
                closestDistance = distanceToBack;
                closestPoint = new Vector3(point.x, point.y, minimum.z);
            }

            if (distanceToFront < closestDistance)
            {
                closestPoint = new Vector3(point.x, point.y, maximum.z);
            }

            return closestPoint;
        }
    }
}
