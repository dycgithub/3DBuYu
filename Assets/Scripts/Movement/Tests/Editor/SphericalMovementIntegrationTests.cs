using NUnit.Framework;
using SphereMovement.Core;
using SphereMovement.Input;
using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Tests
{
    /// <summary>
    /// 球面移动系统集成测试
    /// </summary>
    public class SphericalMovementIntegrationTests
    {
        private const float Epsilon = 0.001f;

        [Test]
        public void FullMovementCycle_NorthPoleToEquator_CorrectPosition()
        {
            // 设置系统
            var calculator = new SphericalPositionCalculator
            {
                SphereCenter = Vector3.zero,
                SphereRadius = 5f
            };

            var mockInput = new MockInputProvider();
            var inputHandler = new MovementInputHandler(mockInput)
            {
                MoveSpeed = 90f // 90度/秒
            };

            var smoothController = new SmoothMovementController
            {
                UseSmoothMovement = false // 禁用平滑以便精确测试
            };

            // 从北极开始
            Vector2 currentCoords = new Vector2(0f, Mathf.PI / 2f - 0.01f); // 接近北极
            Vector2 targetCoords = currentCoords;

            // 模拟向南移动（减少纬度）
            mockInput.SetVertical(-1f);

            // 模拟1秒
            Vector2 delta = inputHandler.ProcessInput(1f);
            targetCoords += delta;
            targetCoords.y = MovementInputHandler.ClampLatitude(targetCoords.y);

            currentCoords = smoothController.SmoothMove(currentCoords, targetCoords);

            // 验证位置
            Vector3 position = calculator.CalculatePosition(currentCoords);

            // 纬度应该减少（向南移动）
            Assert.That(currentCoords.y, Is.LessThan(Mathf.PI / 2f - 0.01f), "纬度应该减少");

            // 位置应该在球面上
            Assert.That(position.magnitude, Is.EqualTo(5f).Within(Epsilon), "位置应该在球面上");
        }

        [Test]
        public void FullMovementCycle_EquatorEastward_CorrectLongitude()
        {
            var calculator = new SphericalPositionCalculator
            {
                SphereCenter = Vector3.zero,
                SphereRadius = 5f
            };

            var mockInput = new MockInputProvider();
            var inputHandler = new MovementInputHandler(mockInput)
            {
                MoveSpeed = 90f
            };

            var smoothController = new SmoothMovementController
            {
                UseSmoothMovement = false
            };

            // 从本初子午线赤道开始
            Vector2 currentCoords = Vector2.zero;
            Vector2 targetCoords = currentCoords;

            // 向东移动（增加经度）
            mockInput.SetHorizontal(1f);

            Vector2 delta = inputHandler.ProcessInput(1f);
            targetCoords.x += delta.x;
            currentCoords = smoothController.SmoothMove(currentCoords, targetCoords);

            // 验证
            Assert.That(currentCoords.x, Is.GreaterThan(0f), "经度应该增加（向东）");

            Vector3 position = calculator.CalculatePosition(currentCoords);
            Assert.That(position.magnitude, Is.EqualTo(5f).Within(Epsilon));
        }

        [Test]
        public void MovementSystem_AllDirections_MaintainsSpherePosition()
        {
            var calculator = new SphericalPositionCalculator
            {
                SphereCenter = new Vector3(10f, 20f, 30f),
                SphereRadius = 7f
            };

            var mockInput = new MockInputProvider();
            var inputHandler = new MovementInputHandler(mockInput)
            {
                MoveSpeed = 60f
            };

            var smoothController = new SmoothMovementController
            {
                UseSmoothMovement = true,
                SmoothTime = 0.05f
            };

            // 随机移动序列
            Vector2 currentCoords = Vector2.zero;
            Vector2 targetCoords = currentCoords;

            System.Random random = new System.Random(42);
            for (int step = 0; step < 50; step++)
            {
                // 随机输入
                float h = (float)(random.NextDouble() * 2 - 1);
                float v = (float)(random.NextDouble() * 2 - 1);
                mockInput.SetHorizontal(h);
                mockInput.SetVertical(v);

                // 处理输入
                Vector2 delta = inputHandler.ProcessInput(0.016f); // 约60fps
                targetCoords += delta;
                targetCoords.y = MovementInputHandler.ClampLatitude(targetCoords.y);

                // 平滑移动
                currentCoords = smoothController.SmoothMove(currentCoords, targetCoords);

                // 验证位置始终在球面上
                Vector3 position = calculator.CalculatePosition(currentCoords);
                float distanceFromCenter = Vector3.Distance(position, calculator.SphereCenter);

                Assert.That(distanceFromCenter, Is.EqualTo(calculator.SphereRadius).Within(0.01f),
                    $"第{step}步：物体不在球面上，距离={distanceFromCenter}");
            }
        }

        [Test]
        public void MovementSystem_PoleTraversal_SmoothTransition()
        {
            var calculator = new SphericalPositionCalculator
            {
                SphereCenter = Vector3.zero,
                SphereRadius = 5f
            };

            var mockInput = new MockInputProvider();
            var inputHandler = new MovementInputHandler(mockInput)
            {
                MoveSpeed = 45f
            };

            var smoothController = new SmoothMovementController
            {
                UseSmoothMovement = true,
                SmoothTime = 0.1f
            };

            // 从接近南极开始
            Vector2 currentCoords = new Vector2(0f, -Mathf.PI / 2f + 0.1f);
            Vector2 targetCoords = currentCoords;

            // 记录位置变化
            Vector3 previousPosition = calculator.CalculatePosition(currentCoords);
            float totalDistance = 0f;

            // 向北移动穿过南极区域
            mockInput.SetVertical(1f);

            for (int step = 0; step < 100; step++)
            {
                Vector2 delta = inputHandler.ProcessInput(0.016f);
                targetCoords += delta;
                targetCoords.y = MovementInputHandler.ClampLatitude(targetCoords.y);

                currentCoords = smoothController.SmoothMove(currentCoords, targetCoords);

                Vector3 position = calculator.CalculatePosition(currentCoords);
                totalDistance += Vector3.Distance(previousPosition, position);
                previousPosition = position;

                // 验证始终在球面上
                Assert.That(position.magnitude, Is.EqualTo(5f).Within(0.01f));
            }

            // 应该移动了一段距离
            Assert.That(totalDistance, Is.GreaterThan(1f), "应该移动了一段距离");
        }
    }
}
