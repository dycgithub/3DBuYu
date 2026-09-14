using CameraSystem;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证区域摄像机方向的球面覆盖半径，确保距离计算不会依赖手工填写的覆盖值。
    /// </summary>
    public sealed class SphericalCameraCoverageTests
    {
        [Test]
        public void TetrahedronDirections_ReturnTheExpectedCoverageRadius()
        {
            Vector3[] directions = SphericalCameraRegional.BuildRegionDirections(4);

            float coverage = SphericalCameraRegional.CalculateCoverageRadius(directions);
            float expected = Mathf.Acos(1f / 3f) * Mathf.Rad2Deg;

            Assert.That(coverage, Is.EqualTo(expected).Within(0.05f));
        }

        [Test]
        public void OctahedronDirections_ReturnTheExpectedCoverageRadius()
        {
            Vector3[] directions = SphericalCameraRegional.BuildRegionDirections(6);

            float coverage = SphericalCameraRegional.CalculateCoverageRadius(directions);
            float expected = Mathf.Acos(1f / Mathf.Sqrt(3f)) * Mathf.Rad2Deg;

            Assert.That(coverage, Is.EqualTo(expected).Within(0.05f));
        }

        [Test]
        public void CubeDirections_ReturnTheExpectedCoverageRadius()
        {
            Vector3[] directions = SphericalCameraRegional.BuildRegionDirections(8);

            float coverage = SphericalCameraRegional.CalculateCoverageRadius(directions);
            float expected = Mathf.Acos(1f / Mathf.Sqrt(3f)) * Mathf.Rad2Deg;

            Assert.That(coverage, Is.EqualTo(expected).Within(0.05f));
        }

        [Test]
        public void CameraDistance_UsesTheHorizonWhenFovCanSeeTheCoverageEdge()
        {
            float distance = SphericalCameraRegional.CalculateCameraDistance(25f, 40f, 60f);

            float expected = 25f / Mathf.Cos(60f * Mathf.Deg2Rad);
            Assert.That(distance, Is.EqualTo(expected).Within(0.001f));
        }
    }
}
