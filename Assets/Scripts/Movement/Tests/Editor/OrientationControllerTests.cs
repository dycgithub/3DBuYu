using NUnit.Framework;
using SphereMovement.Core;
using UnityEngine;

namespace SphereMovement.Tests
{
    /// <summary>
    /// OrientationController 测试
    /// </summary>
    public class OrientationControllerTests
    {
        private OrientationController _controller;
        private GameObject _testObject;
        private const float Epsilon = 0.001f;

        [SetUp]
        public void Setup()
        {
            _controller = new OrientationController
            {
                SphereCenter = Vector3.zero
            };

            _testObject = new GameObject("TestObject");
            _testObject.transform.position = new Vector3(0f, 5f, 0f); // 北极
        }

        [TearDown]
        public void TearDown()
        {
            if (_testObject != null)
            {
                Object.DestroyImmediate(_testObject);
            }
        }

        #region Properties

        [Test]
        public void SphereCenter_Default_IsZero()
        {
            var controller = new OrientationController();
            Assert.AreEqual(Vector3.zero, controller.SphereCenter);
        }

        [Test]
        public void PoleThreshold_Default_IsSet()
        {
            var controller = new OrientationController();
            Assert.That(controller.PoleThreshold, Is.GreaterThan(0f));
        }

        [Test]
        public void PoleTransitionSpeed_Default_IsSet()
        {
            var controller = new OrientationController();
            Assert.That(controller.PoleTransitionSpeed, Is.GreaterThan(0f));
        }

        #endregion

        #region GetLatitudeTangent

        [Test]
        public void GetLatitudeTangent_AtEquator_ReturnsEastDirection()
        {
            Vector3 equatorPos = new Vector3(0f, 0f, 1f);
            Vector3 tangent = _controller.GetLatitudeTangent(equatorPos);

            // 在赤道(0,0,1)，纬线切线应该是东西方向 (1,0,0)
            Assert.That(tangent.x, Is.GreaterThan(0.9f));
            Assert.That(Mathf.Abs(tangent.y), Is.LessThan(Epsilon));
            Assert.That(Mathf.Abs(tangent.z), Is.LessThan(Epsilon));
        }

        [Test]
        public void GetLatitudeTangent_ReturnsUnitVector()
        {
            Vector3[] testPositions = new[]
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, -1f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(0.707f, 0.707f, 0f),
                new Vector3(0.577f, 0.577f, 0.577f)
            };

            foreach (var pos in testPositions)
            {
                Vector3 normalizedPos = pos.normalized;
                Vector3 tangent = _controller.GetLatitudeTangent(normalizedPos);

                Assert.That(tangent.magnitude, Is.EqualTo(1f).Within(Epsilon),
                    $"位置 {normalizedPos} 的切线应该是单位向量");
            }
        }

        [Test]
        public void GetLatitudeTangent_IsPerpendicularToPosition()
        {
            Vector3[] testPositions = new[]
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0.707f, 0.707f, 0f)
            };

            foreach (var pos in testPositions)
            {
                Vector3 normalizedPos = pos.normalized;
                Vector3 tangent = _controller.GetLatitudeTangent(normalizedPos);

                float dot = Vector3.Dot(normalizedPos, tangent);
                Assert.That(Mathf.Abs(dot), Is.LessThan(Epsilon),
                    $"位置 {normalizedPos} 的切线应该与之垂直，点积={dot}");
            }
        }

        #endregion

        #region GetLongitudeTangent

        [Test]
        public void GetLongitudeTangent_ReturnsUnitVector()
        {
            Vector3 pos = new Vector3(0f, 0f, 1f).normalized;
            Vector3 tangent = _controller.GetLongitudeTangent(pos);

            Assert.That(tangent.magnitude, Is.EqualTo(1f).Within(Epsilon));
        }

        [Test]
        public void GetLongitudeTangent_IsPerpendicularToPosition()
        {
            Vector3 pos = new Vector3(0.577f, 0.577f, 0.577f).normalized;
            Vector3 tangent = _controller.GetLongitudeTangent(pos);

            float dot = Vector3.Dot(pos, tangent);
            Assert.That(Mathf.Abs(dot), Is.LessThan(Epsilon));
        }

        #endregion

        #region UpdateOrientation

        [Test]
        public void UpdateOrientation_AtNorthPole_UpPointsToCenter()
        {
            _testObject.transform.position = new Vector3(0f, 5f, 0f);
            Vector2 coords = new Vector2(0f, Mathf.PI / 2f);

            _controller.UpdateOrientation(
                _testObject.transform,
                Vector3.up,
                coords
            );

            // 在北极，物体的上方向应该指向球心（向下）
            Vector3 up = _testObject.transform.up;
            Assert.That(up.y, Is.LessThan(-0.9f), "北极物体的上方向应该接近-Vector3.up");
        }

        [Test]
        public void UpdateOrientation_AtEquator_UpPointsToCenter()
        {
            _testObject.transform.position = new Vector3(0f, 0f, 5f);
            Vector2 coords = Vector2.zero;

            _controller.UpdateOrientation(
                _testObject.transform,
                Vector3.forward,
                coords
            );

            // 在赤道，物体的上方向应该指向球心（向后）
            Vector3 up = _testObject.transform.up;
            Assert.That(up.z, Is.LessThan(-0.9f), "赤道物体的上方向应该接近-Vector3.forward");
        }

        #endregion
    }
}
