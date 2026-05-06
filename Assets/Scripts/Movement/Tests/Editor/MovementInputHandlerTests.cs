using NUnit.Framework;
using SphereMovement.Core;
using SphereMovement.Input;
using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Tests
{
    /// <summary>
    /// MovementInputHandler 测试
    /// </summary>
    public class MovementInputHandlerTests
    {
        private MockInputProvider _mockInput;
        private MovementInputHandler _handler;

        [SetUp]
        public void Setup()
        {
            _mockInput = new MockInputProvider();
            _handler = new MovementInputHandler(_mockInput)
            {
                MoveSpeed = 30f
            };
        }

        [Test]
        public void Constructor_NullInputProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() => new MovementInputHandler(null));
        }

        [Test]
        public void HasActiveInput_NoInput_ReturnsFalse()
        {
            _mockInput.Clear();
            Assert.IsFalse(_handler.HasActiveInput);
        }

        [Test]
        public void HasActiveInput_WithInput_ReturnsTrue()
        {
            _mockInput.SetHorizontal(1f);
            Assert.IsTrue(_handler.HasActiveInput);
        }

        [Test]
        public void ProcessInput_NoInput_ReturnsZero()
        {
            _mockInput.Clear();
            Vector2 result = _handler.ProcessInput(1f);
            Assert.AreEqual(Vector2.zero, result);
        }

        [Test]
        public void ProcessInput_RightMovement_PositiveLongitudeDelta()
        {
            _mockInput.SetHorizontal(1f);
            Vector2 result = _handler.ProcessInput(1f);

            Assert.That(result.x, Is.GreaterThan(0), "右移应该增加经度");
            Assert.That(result.y, Is.EqualTo(0).Within(0.0001f), "纬度不应该改变");
        }

        [Test]
        public void ProcessInput_LeftMovement_NegativeLongitudeDelta()
        {
            _mockInput.SetHorizontal(-1f);
            Vector2 result = _handler.ProcessInput(1f);

            Assert.That(result.x, Is.LessThan(0), "左移应该减少经度");
        }

        [Test]
        public void ProcessInput_UpMovement_PositiveLatitudeDelta()
        {
            _mockInput.SetVertical(1f);
            Vector2 result = _handler.ProcessInput(1f);

            Assert.That(result.y, Is.GreaterThan(0), "上移应该增加纬度");
            Assert.That(result.x, Is.EqualTo(0).Within(0.0001f), "经度不应该改变");
        }

        [Test]
        public void ProcessInput_DownMovement_NegativeLatitudeDelta()
        {
            _mockInput.SetVertical(-1f);
            Vector2 result = _handler.ProcessInput(1f);

            Assert.That(result.y, Is.LessThan(0), "下移应该减少纬度");
        }

        [Test]
        public void ProcessInput_SpeedAffectsDelta()
        {
            _mockInput.SetHorizontal(1f);

            _handler.MoveSpeed = 30f;
            Vector2 result1 = _handler.ProcessInput(1f);

            _handler.MoveSpeed = 60f;
            Vector2 result2 = _handler.ProcessInput(1f);

            Assert.That(result2.x, Is.GreaterThan(result1.x), "速度翻倍应该产生更大的变化");
            Assert.That(result2.x, Is.EqualTo(result1.x * 2).Within(0.0001f), "变化量应该与速度成比例");
        }

        [Test]
        public void ProcessInput_DeltaTimeAffectsDelta()
        {
            _mockInput.SetHorizontal(1f);

            Vector2 result1 = _handler.ProcessInput(0.5f);
            Vector2 result2 = _handler.ProcessInput(1f);

            Assert.That(result2.x, Is.GreaterThan(result1.x), "更长的deltaTime应该产生更大的变化");
            Assert.That(result2.x, Is.EqualTo(result1.x * 2).Within(0.0001f), "变化量应该与deltaTime成比例");
        }

        #region ClampLatitude Tests

        [Test]
        public void ClampLatitude_ValidLatitude_ReturnsSameValue()
        {
            float input = 0f;
            float result = MovementInputHandler.ClampLatitude(input);
            Assert.AreEqual(input, result, 0.0001f);
        }

        [Test]
        public void ClampLatitude_NorthPole_ReturnsMaxLatitude()
        {
            float input = Mathf.PI / 2f;
            float result = MovementInputHandler.ClampLatitude(input);
            Assert.That(result, Is.LessThan(Mathf.PI / 2f));
        }

        [Test]
        public void ClampLatitude_SouthPole_ReturnsMinLatitude()
        {
            float input = -Mathf.PI / 2f;
            float result = MovementInputHandler.ClampLatitude(input);
            Assert.That(result, Is.GreaterThan(-Mathf.PI / 2f));
        }

        #endregion
    }
}
