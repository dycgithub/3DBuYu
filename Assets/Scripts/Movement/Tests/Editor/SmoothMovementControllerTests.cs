using NUnit.Framework;
using SphereMovement.Core;
using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Tests
{
    /// <summary>
    /// SmoothMovementController 测试
    /// </summary>
    public class SmoothMovementControllerTests
    {
        private SmoothMovementController _controller;

        [SetUp]
        public void Setup()
        {
            _controller = new SmoothMovementController
            {
                UseSmoothMovement = true,
                SmoothTime = 0.1f
            };
        }

        #region Property Tests

        [Test]
        public void UseSmoothMovement_DefaultValue_IsTrue()
        {
            var controller = new SmoothMovementController();
            Assert.IsTrue(controller.UseSmoothMovement);
        }

        [Test]
        public void SmoothTime_DefaultValue_IsSet()
        {
            var controller = new SmoothMovementController();
            Assert.That(controller.SmoothTime, Is.GreaterThan(0f));
        }

        #endregion

        #region SmoothMove Tests

        [Test]
        public void SmoothMove_DisabledSmoothing_ReturnsTarget()
        {
            _controller.UseSmoothMovement = false;
            Vector2 current = Vector2.zero;
            Vector2 target = new Vector2(10f, 10f);

            Vector2 result = _controller.SmoothMove(current, target);

            Assert.AreEqual(target, result);
        }

        [Test]
        public void SmoothMove_SameCurrentAndTarget_ReturnsSame()
        {
            Vector2 current = new Vector2(5f, 5f);
            Vector2 target = new Vector2(5f, 5f);

            Vector2 result = _controller.SmoothMove(current, target);

            Assert.That(result.x, Is.EqualTo(target.x).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(target.y).Within(0.0001f));
        }

        [Test]
        public void SmoothMove_MovesTowardsTarget()
        {
            Vector2 current = Vector2.zero;
            Vector2 target = new Vector2(10f, 0f);

            Vector2 result = _controller.SmoothMove(current, target);

            // 应该向目标移动，但不直接到达
            Assert.That(result.x, Is.GreaterThan(current.x));
            Assert.That(result.x, Is.LessThan(target.x));
        }

        [Test]
        public void SmoothMove_MultipleCalls_ApproachTarget()
        {
            Vector2 current = Vector2.zero;
            Vector2 target = new Vector2(10f, 10f);

            // 模拟多次Update调用
            for (int i = 0; i < 100; i++)
            {
                current = _controller.SmoothMove(current, target);
            }

            // 应该非常接近目标
            Assert.That(current.x, Is.EqualTo(target.x).Within(0.01f));
            Assert.That(current.y, Is.EqualTo(target.y).Within(0.01f));
        }

        [Test]
        public void SmoothMove_DifferentSmoothTimes_AffectSpeed()
        {
            Vector2 current1 = Vector2.zero;
            Vector2 current2 = Vector2.zero;
            Vector2 target = new Vector2(10f, 0f);

            var fastController = new SmoothMovementController
            {
                SmoothTime = 0.05f,
                UseSmoothMovement = true
            };

            var slowController = new SmoothMovementController
            {
                SmoothTime = 0.5f,
                UseSmoothMovement = true
            };

            // 单次移动
            Vector2 result1 = fastController.SmoothMove(current1, target);
            Vector2 result2 = slowController.SmoothMove(current2, target);

            // 平滑时间越短，移动越快
            Assert.That(result1.x, Is.GreaterThan(result2.x),
                "平滑时间较短的应该移动更远");
        }

        #endregion

        #region Reset Tests

        [Test]
        public void Reset_AfterSmoothing_StartsFresh()
        {
            Vector2 current = Vector2.zero;
            Vector2 target = new Vector2(10f, 10f);

            // 先进行一部分移动
            for (int i = 0; i < 10; i++)
            {
                current = _controller.SmoothMove(current, target);
            }

            // 重置
            _controller.Reset();

            // 从新位置开始平滑
            Vector2 newTarget = new Vector2(20f, 20f);
            Vector2 result = _controller.SmoothMove(current, newTarget);

            // 应该向新目标移动
            Assert.That(result.x, Is.GreaterThan(current.x));
        }

        #endregion
    }
}
