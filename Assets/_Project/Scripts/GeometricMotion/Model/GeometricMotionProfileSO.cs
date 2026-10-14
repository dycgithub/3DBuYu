using UnityEngine;

namespace GeometricMotion
{
    /// <summary>
    /// 几何运动的静态表现配置。
    /// <para>资产只保存设计参数，运行时会转换为 Burst 可读取的配置结构。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "GeometricMotionProfile", menuName = "Motion/Geometric Motion Profile")]
    public sealed class GeometricMotionProfileSO : ScriptableObject
    {
        [Header("Reference")]
        [SerializeField, Min(0.001f)] private float referenceSpeed = 5f;
        [SerializeField, Min(0.001f)] private float referenceAcceleration = 10f;

        [Header("Shape")]
        [SerializeField, Min(0f)] private float travelStretch = 0.35f;
        [SerializeField, Min(0f)] private float accelerationStretch = 0.15f;
        [SerializeField, Range(0f, 0.95f)] private float prepareSquash = 0.25f;
        [SerializeField, Min(0f)] private float releaseStretch = 0.45f;
        [SerializeField, Range(0f, 0.95f)] private float impactSquash = 0.35f;
        [SerializeField, Min(0f)] private float reboundStretch = 0.2f;
        [SerializeField] private GeometricMotionAxis axis = GeometricMotionAxis.Z;

        [Header("Follow Through")]
        [SerializeField, Min(0f)] private float followOffset = 0.08f;
        [SerializeField, Min(0f)] private float followTilt = 0.12f;
        [SerializeField, Min(0f)] private float followResponse = 10f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float prepareDuration = 0.12f;
        [SerializeField] private GeometricMotionEase prepareEase = GeometricMotionEase.EaseInOut;
        [SerializeField, Min(0f)] private float releaseDuration = 0.12f;
        [SerializeField] private GeometricMotionEase releaseEase = GeometricMotionEase.EaseOut;
        [SerializeField, Min(0f)] private float impactDuration = 0.08f;
        [SerializeField] private GeometricMotionEase impactEase = GeometricMotionEase.EaseOut;
        [SerializeField, Min(0f)] private float reboundDuration = 0.12f;
        [SerializeField] private GeometricMotionEase reboundEase = GeometricMotionEase.BackOut;
        [SerializeField, Min(0f)] private float settleDuration = 0.18f;
        [SerializeField] private GeometricMotionEase settleEase = GeometricMotionEase.EaseOut;

        /// <summary>将 Inspector 参数转换为 ECS 和 GameObject 共用的配置。</summary>
        /// <returns>经过边界归一化的几何运动配置。</returns>
        public GeometricMotionConfig CreateConfig()
        {
            return new GeometricMotionConfig
            {
                ReferenceSpeed = referenceSpeed,
                ReferenceAcceleration = referenceAcceleration,
                TravelStretch = travelStretch,
                AccelerationStretch = accelerationStretch,
                PrepareSquash = prepareSquash,
                ReleaseStretch = releaseStretch,
                ImpactSquash = impactSquash,
                ReboundStretch = reboundStretch,
                FollowOffset = followOffset,
                FollowTilt = followTilt,
                FollowResponse = followResponse,
                PrepareDuration = prepareDuration,
                ReleaseDuration = releaseDuration,
                ImpactDuration = impactDuration,
                ReboundDuration = reboundDuration,
                SettleDuration = settleDuration,
                PrepareEase = prepareEase,
                ReleaseEase = releaseEase,
                ImpactEase = impactEase,
                ReboundEase = reboundEase,
                SettleEase = settleEase,
                Axis = axis,
            }.Normalize();
        }
    }
}
