using NUnit.Framework;
using SphereMovement;
using UnityEngine;

namespace SphereMovement.Tests
{
    /// <summary>
    /// SphericalCoordinates 静态工具类测试
    /// </summary>
    public class SphericalCoordinatesTests
    {
        private const float Epsilon = 0.0001f;

        #region Cartesian to Spherical

        [Test]
        public void FromCartesian_NorthPole_ReturnsCorrectLatitude()
        {
            Vector3 northPole = Vector3.up;
            Vector2 spherical = SphericalCoordinates.FromCartesian(northPole);

            Assert.AreEqual(0f, spherical.x, Epsilon, "经度应该为0");
            Assert.AreEqual(Mathf.PI / 2f, spherical.y, Epsilon, "纬度应该是PI/2（北极）");
        }

        [Test]
        public void FromCartesian_SouthPole_ReturnsCorrectLatitude()
        {
            Vector3 southPole = Vector3.down;
            Vector2 spherical = SphericalCoordinates.FromCartesian(southPole);

            Assert.AreEqual(0f, spherical.x, Epsilon, "经度应该为0");
            Assert.AreEqual(-Mathf.PI / 2f, spherical.y, Epsilon, "纬度应该是-PI/2（南极）");
        }

        [Test]
        public void FromCartesian_EquatorPrimeMeridian_ReturnsZero()
        {
            Vector3 point = new Vector3(0f, 0f, 1f);
            Vector2 spherical = SphericalCoordinates.FromCartesian(point);

            Assert.AreEqual(0f, spherical.x, Epsilon, "经度应该为0");
            Assert.AreEqual(0f, spherical.y, Epsilon, "纬度应该为0");
        }

        [Test]
        public void FromCartesian_Equator90Degrees_ReturnsCorrectLongitude()
        {
            Vector3 point = new Vector3(1f, 0f, 0f);
            Vector2 spherical = SphericalCoordinates.FromCartesian(point);

            Assert.AreEqual(Mathf.PI / 2f, spherical.x, Epsilon, "经度应该是PI/2");
            Assert.AreEqual(0f, spherical.y, Epsilon, "纬度应该为0");
        }

        #endregion

        #region Spherical to Cartesian

        [Test]
        public void ToCartesian_ZeroZero_ReturnsUnitZ()
        {
            Vector2 spherical = Vector2.zero;
            Vector3 cartesian = SphericalCoordinates.ToCartesian(spherical);

            Assert.AreEqual(0f, cartesian.x, Epsilon, "X应该为0");
            Assert.AreEqual(0f, cartesian.y, Epsilon, "Y应该为0");
            Assert.AreEqual(1f, cartesian.z, Epsilon, "Z应该为1");
        }

        [Test]
        public void ToCartesian_NorthPole_ReturnsUnitY()
        {
            Vector2 spherical = new Vector2(0f, Mathf.PI / 2f);
            Vector3 cartesian = SphericalCoordinates.ToCartesian(spherical);

            Assert.AreEqual(0f, cartesian.x, Epsilon, "X应该为0");
            Assert.AreEqual(1f, cartesian.y, Epsilon, "Y应该为1");
            Assert.AreEqual(0f, cartesian.z, Epsilon, "Z应该为0");
        }

        [Test]
        public void ToCartesian_SouthPole_ReturnsNegativeUnitY()
        {
            Vector2 spherical = new Vector2(0f, -Mathf.PI / 2f);
            Vector3 cartesian = SphericalCoordinates.ToCartesian(spherical);

            Assert.AreEqual(0f, cartesian.x, Epsilon, "X应该为0");
            Assert.AreEqual(-1f, cartesian.y, Epsilon, "Y应该为-1");
            Assert.AreEqual(0f, cartesian.z, Epsilon, "Z应该为0");
        }

        #endregion

        #region Round Trip

        [Test]
        public void RoundTrip_RandomPoints_MatchesOriginal([Random(10)] int seed)
        {
            Random.InitState(seed);

            for (int i = 0; i < 100; i++)
            {
                // 生成随机球坐标
                float longitude = Random.Range(-Mathf.PI, Mathf.PI);
                float latitude = Random.Range(-Mathf.PI / 2f + 0.01f, Mathf.PI / 2f - 0.01f);

                Vector2 original = new Vector2(longitude, latitude);

                // 转换为笛卡尔坐标
                Vector3 cartesian = SphericalCoordinates.ToCartesian(original);

                // 转换回球坐标
                Vector2 result = SphericalCoordinates.FromCartesian(cartesian);

                // 验证（注意经度可能有2PI的差异）
                float longitudeDiff = Mathf.Abs(Mathf.DeltaAngle(result.x * Mathf.Rad2Deg, original.x * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                Assert.That(longitudeDiff, Is.LessThan(Epsilon), $"经度不匹配：原始={original.x}，结果={result.x}");
                Assert.That(result.y, Is.EqualTo(original.y).Within(Epsilon), $"纬度不匹配：原始={original.y}，结果={result.y}");
            }
        }

        #endregion
    }
}
