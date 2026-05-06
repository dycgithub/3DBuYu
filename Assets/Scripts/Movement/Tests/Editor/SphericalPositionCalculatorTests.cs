using NUnit.Framework;
using SphereMovement.Core;
using UnityEngine;

namespace SphereMovement.Tests
{
    /// <summary>
    /// SphericalPositionCalculator 测试
    /// </summary>
    public class SphericalPositionCalculatorTests
    {
        private SphericalPositionCalculator _calculator;
        private const float Epsilon = 0.0001f;

        [SetUp]
        public void Setup()
        {
            _calculator = new SphericalPositionCalculator
            {
                SphereCenter = Vector3.zero,
                SphereRadius = 5f
            };
        }

        #region Properties

        [Test]
        public void SphereCenter_Default_IsZero()
        {
            var calc = new SphericalPositionCalculator();
            Assert.AreEqual(Vector3.zero, calc.SphereCenter);
        }

        [Test]
        public void SphereRadius_Default_IsPositive()
        {
            var calc = new SphericalPositionCalculator();
            Assert.That(calc.SphereRadius, Is.GreaterThan(0f));
        }

        [Test]
        public void SphereCenter_SetValue_UpdatesCorrectly()
        {
            Vector3 newCenter = new Vector3(10f, 20f, 30f);
            _calculator.SphereCenter = newCenter;
            Assert.AreEqual(newCenter, _calculator.SphereCenter);
        }

        [Test]
        public void SphereRadius_SetValue_UpdatesCorrectly()
        {
            _calculator.SphereRadius = 10f;
            Assert.AreEqual(10f, _calculator.SphereRadius);
        }

        #endregion

        #region CalculateNormalizedPosition

        [Test]
        public void CalculateNormalizedPosition_ZeroCoords_ReturnsUnitZ()
        {
            Vector2 coords = Vector2.zero;
            Vector3 result = _calculator.CalculateNormalizedPosition(coords);

            Assert.AreEqual(0f, result.x, Epsilon);
            Assert.AreEqual(0f, result.y, Epsilon);
            Assert.AreEqual(1f, result.z, Epsilon);
            Assert.AreEqual(1f, result.magnitude, Epsilon);
        }

        [Test]
        public void CalculateNormalizedPosition_NorthPole_ReturnsUnitY()
        {
            Vector2 coords = new Vector2(0f, Mathf.PI / 2f);
            Vector3 result = _calculator.CalculateNormalizedPosition(coords);

            Assert.AreEqual(0f, result.x, Epsilon);
            Assert.AreEqual(1f, result.y, Epsilon);
            Assert.AreEqual(0f, result.z, Epsilon);
            Assert.AreEqual(1f, result.magnitude, Epsilon);
        }

        [Test]
        public void CalculateNormalizedPosition_ReturnsUnitVector()
        {
            Vector2[] testCoords = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(Mathf.PI / 2f, 0f),
                new Vector2(Mathf.PI, 0f),
                new Vector2(-Mathf.PI / 2f, 0f),
                new Vector2(0f, Mathf.PI / 4f),
                new Vector2(0f, -Mathf.PI / 4f),
                new Vector2(Mathf.PI / 4f, Mathf.PI / 6f),
                new Vector2(-Mathf.PI * 0.75f, -Mathf.PI / 3f)
            };

            foreach (var coords in testCoords)
            {
                Vector3 result = _calculator.CalculateNormalizedPosition(coords);
                Assert.AreEqual(1f, result.magnitude, Epsilon,
                    $"坐标 {coords} 应该返回单位向量，但得到长度 {result.magnitude}");
            }
        }

        #endregion

        #region CalculatePosition

        [Test]
        public void CalculatePosition_AppliesRadiusAndCenter()
        {
            _calculator.SphereCenter = new Vector3(10f, 20f, 30f);
            _calculator.SphereRadius = 5f;

            Vector2 coords = Vector2.zero;
            Vector3 result = _calculator.CalculatePosition(coords);

            // 单位球面上 (0,0,1)，缩放后应为 (0,0,5)，再加上中心点
            Vector3 expected = new Vector3(10f, 20f, 35f);
            Assert.That(result.x, Is.EqualTo(expected.x).Within(Epsilon));
            Assert.That(result.y, Is.EqualTo(expected.y).Within(Epsilon));
            Assert.That(result.z, Is.EqualTo(expected.z).Within(Epsilon));
        }

        [Test]
        public void CalculatePosition_DifferentRadius_ScalesCorrectly()
        {
            Vector2 coords = new Vector2(0f, Mathf.PI / 2f); // 北极

            _calculator.SphereRadius = 1f;
            Vector3 result1 = _calculator.CalculatePosition(coords);

            _calculator.SphereRadius = 5f;
            Vector3 result2 = _calculator.CalculatePosition(coords);

            // 应该是线性缩放
            Assert.That(result2.y, Is.EqualTo(result1.y * 5f).Within(Epsilon));
        }

        #endregion

        #region CartesianToSpherical

        [Test]
        public void CartesianToSpherical_RoundTrip_WithCalculateNormalizedPosition()
        {
            // 从球坐标开始
            Vector2 originalCoords = new Vector2(Mathf.PI / 4f, Mathf.PI / 6f);

            // 计算笛卡尔坐标
            Vector3 cartesian = _calculator.CalculateNormalizedPosition(originalCoords);

            // 转换回球坐标
            Vector2 resultCoords = _calculator.CartesianToSpherical(cartesian);

            // 验证（经度可能有2PI的差异）
            float longitudeDiff = Mathf.Abs(Mathf.DeltaAngle(resultCoords.x * Mathf.Rad2Deg, originalCoords.x * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            Assert.That(longitudeDiff, Is.LessThan(Epsilon), "经度应该匹配");
            Assert.That(resultCoords.y, Is.EqualTo(originalCoords.y).Within(Epsilon), "纬度应该匹配");
        }

        #endregion
    }
}
