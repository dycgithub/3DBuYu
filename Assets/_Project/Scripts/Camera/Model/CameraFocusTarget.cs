using UnityEngine;

namespace CameraSystem
{
    /// <summary>描述 Follow 摄像机当前是否需要关注一个战斗目标。</summary>
    public enum CameraFocusMode
    {
        None,
        LockedTarget
    }

    /// <summary>
    /// 传递给 Follow 摄像机的短生命周期构图目标。
    /// 位置由编排器从现有战斗目标服务读取，Follow 不依赖战斗领域类型。
    /// </summary>
    public readonly struct CameraFocusTarget
    {
        /// <summary>不使用战斗目标构图。</summary>
        public static CameraFocusTarget None => new(CameraFocusMode.None, Vector3.zero, 0f);

        /// <summary>当前构图模式。</summary>
        public CameraFocusMode Mode { get; }

        /// <summary>目标世界坐标。</summary>
        public Vector3 Position { get; }

        /// <summary>目标对最终注视点的最大混合权重，范围为零到一。</summary>
        public float Weight { get; }

        /// <summary>创建一个构图目标值。</summary>
        /// <param name="mode">构图模式。</param>
        /// <param name="position">目标世界坐标。</param>
        /// <param name="weight">目标混合权重，范围为零到一。</param>
        public CameraFocusTarget(CameraFocusMode mode, Vector3 position, float weight)
        {
            Mode = mode;
            Position = position;
            Weight = float.IsNaN(weight) || float.IsInfinity(weight)
                ? 0f
                : Mathf.Clamp01(weight);
        }
    }
}
