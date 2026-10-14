namespace _Project.UI.EnemyThreatMarker
{
    /// <summary>根据进入与退出半径计算威胁标记的滞回状态。</summary>
    internal static class EnemyThreatRange
    {
        /// <summary>判断指定距离下标记是否应保持显示。</summary>
        internal static bool ShouldMark(
            float distanceSquared,
            bool isMarked,
            float enterRadiusSquared,
            float exitRadiusSquared)
        {
            float threshold = isMarked ? exitRadiusSquared : enterRadiusSquared;
            return distanceSquared <= threshold;
        }
    }
}
