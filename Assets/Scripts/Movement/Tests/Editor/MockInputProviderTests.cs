using NUnit.Framework;
using SphereMovement.Input;
using UnityEngine;

namespace SphereMovement.Tests
{
    /// <summary>
    /// MockInputProvider 测试
    /// </summary>
    public class MockInputProviderTests
    {
        private MockInputProvider _provider;

        [SetUp]
        public void Setup()
        {
            _provider = new MockInputProvider();
        }

        #region Initial State

        [Test]
        public void InitialState_AllValuesZero()
        {
            Assert.AreEqual(0f, _provider.Horizontal);
            Assert.AreEqual(0f, _provider.Vertical);
            Assert.IsFalse(_provider.HasInput);
        }

        #endregion

        #region SetHorizontal

        [Test]
        public void SetHorizontal_PositiveValue_ReturnsCorrectValue()
        {
            _provider.SetHorizontal(1f);
            Assert.AreEqual(1f, _provider.Horizontal);
        }

        [Test]
        public void SetHorizontal_NegativeValue_ReturnsCorrectValue()
        {
            _provider.SetHorizontal(-1f);
            Assert.AreEqual(-1f, _provider.Horizontal);
        }

        [Test]
        public void SetHorizontal_ZeroValue_ReturnsZero()
        {
            _provider.SetHorizontal(0f);
            Assert.AreEqual(0f, _provider.Horizontal);
        }

        [Test]
        public void SetHorizontal_DoesNotAffectVertical()
        {
            _provider.SetVertical(0.5f);
            _provider.SetHorizontal(1f);

            Assert.AreEqual(1f, _provider.Horizontal);
            Assert.AreEqual(0.5f, _provider.Vertical);
        }

        #endregion

        #region SetVertical

        [Test]
        public void SetVertical_PositiveValue_ReturnsCorrectValue()
        {
            _provider.SetVertical(1f);
            Assert.AreEqual(1f, _provider.Vertical);
        }

        [Test]
        public void SetVertical_NegativeValue_ReturnsCorrectValue()
        {
            _provider.SetVertical(-1f);
            Assert.AreEqual(-1f, _provider.Vertical);
        }

        [Test]
        public void SetVertical_DoesNotAffectHorizontal()
        {
            _provider.SetHorizontal(0.5f);
            _provider.SetVertical(1f);

            Assert.AreEqual(0.5f, _provider.Horizontal);
            Assert.AreEqual(1f, _provider.Vertical);
        }

        #endregion

        #region HasInput

        [Test]
        public void HasInput_HorizontalNonZero_ReturnsTrue()
        {
            _provider.SetHorizontal(0.1f);
            Assert.IsTrue(_provider.HasInput);
        }

        [Test]
        public void HasInput_VerticalNonZero_ReturnsTrue()
        {
            _provider.SetVertical(0.1f);
            Assert.IsTrue(_provider.HasInput);
        }

        [Test]
        public void HasInput_BothZero_ReturnsFalse()
        {
            _provider.SetHorizontal(0f);
            _provider.SetVertical(0f);
            Assert.IsFalse(_provider.HasInput);
        }

        [Test]
        public void HasInput_BelowDeadZone_ReturnsFalse()
        {
            _provider.SetDeadZone(0.01f);
            _provider.SetHorizontal(0.005f);
            Assert.IsFalse(_provider.HasInput);
        }

        [Test]
        public void HasInput_AboveDeadZone_ReturnsTrue()
        {
            _provider.SetDeadZone(0.01f);
            _provider.SetHorizontal(0.02f);
            Assert.IsTrue(_provider.HasInput);
        }

        #endregion

        #region Clear

        [Test]
        public void Clear_ResetsAllValues()
        {
            _provider.SetHorizontal(1f);
            _provider.SetVertical(1f);

            _provider.Clear();

            Assert.AreEqual(0f, _provider.Horizontal);
            Assert.AreEqual(0f, _provider.Vertical);
            Assert.IsFalse(_provider.HasInput);
        }

        #endregion

        #region DeadZone

        [Test]
        public void SetDeadZone_ChangesThreshold()
        {
            _provider.SetDeadZone(0.1f);
            _provider.SetHorizontal(0.05f);

            Assert.IsFalse(_provider.HasInput);

            _provider.SetDeadZone(0.01f);
            Assert.IsTrue(_provider.HasInput);
        }

        #endregion

        #region Edge Cases

        [Test]
        public void SetHorizontal_VeryLargeValue_HandlesCorrectly()
        {
            _provider.SetHorizontal(1000f);
            Assert.AreEqual(1000f, _provider.Horizontal);
        }

        [Test]
        public void SetHorizontal_VerySmallValue_HandlesCorrectly()
        {
            _provider.SetHorizontal(0.0001f);
            Assert.AreEqual(0.0001f, _provider.Horizontal);
        }

        [Test]
        public void MultipleCalls_LastValueWins()
        {
            _provider.SetHorizontal(0.5f);
            _provider.SetHorizontal(1f);
            _provider.SetHorizontal(-0.5f);

            Assert.AreEqual(-0.5f, _provider.Horizontal);
        }

        #endregion
    }
}
