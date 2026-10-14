using UnityEngine;

namespace Play.FieldContact.Model
{
    /// <summary>供球面显现 Shader 使用的接触源数据。</summary>
    internal readonly struct FieldContactSample
    {
        internal readonly Vector3 Direction;
        internal readonly float SurfaceGap;
        internal readonly float Strength;
        internal readonly int SourceId;

        /// <summary>创建局部方向、表面间距和显现强度样本。</summary>
        /// <param name="direction">从球心指向目标最近点的局部方向。</param>
        /// <param name="surfaceGap">目标与球面的表面间距。</param>
        /// <param name="strength">由表面间距得到的显现强度。</param>
        /// <param name="sourceId">用于合并同一渲染体的标识。</param>
        internal FieldContactSample(
            Vector3 direction,
            float surfaceGap,
            float strength,
            int sourceId)
        {
            Direction = direction;
            SurfaceGap = surfaceGap;
            Strength = strength;
            SourceId = sourceId;
        }
    }
}
