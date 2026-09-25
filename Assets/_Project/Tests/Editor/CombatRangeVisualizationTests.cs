using System.Collections.Generic;
using CombatSystem;
using GameSystem;
using NUnit.Framework;
using Play;
using Play.CombatVisualization.Model;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>验证战斗范围辅助线的几何、配置读取和弹道终点计算基础。</summary>
    public sealed class CombatRangeVisualizationTests
    {
        [Test]
        public void FillCircle_CreatesClosedPointsOnRequestedRadius()
        {
            Vector3 center = new(2f, -3f, 5f);
            Vector3[] points = new Vector3[17];

            SphericalRangeGeometry.FillCircle(
                center,
                4f,
                Vector3.right,
                Vector3.up,
                points);

            Assert.That(points[0], Is.EqualTo(points[^1]).Using(Vector3Comparer.Instance));
            for (int index = 0; index < points.Length - 1; index++)
            {
                Assert.That(
                    Vector3.Distance(center, points[index]),
                    Is.EqualTo(4f).Within(0.0001f));
            }
        }

        [Test]
        public void FillCircle_NormalizesInvalidRadiusToZero()
        {
            Vector3 center = new(1f, 2f, 3f);
            Vector3[] points = new Vector3[5];

            SphericalRangeGeometry.FillCircle(
                center,
                float.NaN,
                Vector3.right,
                Vector3.up,
                points);

            for (int index = 0; index < points.Length; index++)
            {
                Assert.That(points[index], Is.EqualTo(center).Using(Vector3Comparer.Instance));
            }
        }

        [Test]
        public void Detector_ExposesInitializedDetectionRadius()
        {
            GameObject detectorObject = new("Detector");
            try
            {
                PhysicsCentralDetector detector = detectorObject.AddComponent<PhysicsCentralDetector>();
                detector.Initialize(2, 12.5f);

                Assert.That(detector.DetectionRadius, Is.EqualTo(12.5f));
            }
            finally
            {
                Object.DestroyImmediate(detectorObject);
            }
        }

        [Test]
        public void Settings_DefaultsToShowingCombatRangeVisuals()
        {
            SettingsData settings = new();

            Assert.That(settings.showCombatRangeVisuals, Is.True);
        }

        [Test]
        public void Predictor_WithEnoughSamples_ReachesMaximumTravelDistance()
        {
            ProjectileProfileSO profile = ScriptableObject.CreateInstance<ProjectileProfileSO>();
            try
            {
                profile.SpeedUnitsPerSecond = 15f;
                profile.MaximumTravelDistance = 50f;
                Vector3[] points = new Vector3[18];
                float timeStep = 50f / (profile.SpeedUnitsPerSecond * (points.Length - 1));
                ProjectileTrajectoryPredictor predictor = new();

                predictor.PredictTrajectory(
                    profile,
                    50f,
                    Vector3.zero,
                    Vector3.forward,
                    null,
                    points,
                    timeStep);

                Assert.That(points[^1], Is.EqualTo(Vector3.forward * 50f).Using(Vector3Comparer.Instance));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private sealed class Vector3Comparer : IEqualityComparer<Vector3>
        {
            public static readonly Vector3Comparer Instance = new();

            public bool Equals(Vector3 left, Vector3 right)
            {
                return Vector3.Distance(left, right) <= 0.0001f;
            }

            public int GetHashCode(Vector3 value)
            {
                return 0;
            }
        }
    }
}
