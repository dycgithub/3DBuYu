using SphereMovement.Core;
using SphereMovement.Input;
using UnityEngine;

namespace SphereMovement.Tests
{
    /// <summary>
    /// 测试运行器 - 可在编辑器中快速运行基本测试
    /// 注意：完整测试应使用 Unity Test Runner 窗口
    /// </summary>
    public class TestRunner : MonoBehaviour
    {
        [Header("测试选项")]
        [Tooltip("是否自动运行测试")]
        public bool runOnStart = false;

        [Tooltip("是否显示详细日志")]
        public bool verboseLogging = true;

        private void Start()
        {
            if (runOnStart)
            {
                RunBasicTests();
            }
        }

        [ContextMenu("运行基础测试")]
        public void RunBasicTests()
        {
            Debug.Log("=== 开始球面移动系统基础测试 ===");

            int passed = 0;
            int failed = 0;

            // 测试1: 球坐标转换
            if (TestSphericalCoordinates())
            {
                passed++;
                if (verboseLogging) Debug.Log("✓ 球坐标转换测试通过");
            }
            else
            {
                failed++;
                Debug.LogError("✗ 球坐标转换测试失败");
            }

            // 测试2: 位置计算
            if (TestPositionCalculation())
            {
                passed++;
                if (verboseLogging) Debug.Log("✓ 位置计算测试通过");
            }
            else
            {
                failed++;
                Debug.LogError("✗ 位置计算测试失败");
            }

            // 测试3: 输入处理
            if (TestInputHandling())
            {
                passed++;
                if (verboseLogging) Debug.Log("✓ 输入处理测试通过");
            }
            else
            {
                failed++;
                Debug.LogError("✗ 输入处理测试失败");
            }

            // 测试4: 平滑移动
            if (TestSmoothMovement())
            {
                passed++;
                if (verboseLogging) Debug.Log("✓ 平滑移动测试通过");
            }
            else
            {
                failed++;
                Debug.LogError("✗ 平滑移动测试失败");
            }

            Debug.Log($"=== 测试完成：通过 {passed}/{passed + failed} ===");

            if (failed > 0)
            {
                Debug.LogWarning($"有 {failed} 个测试失败，请查看详细日志");
            }
        }

        private bool TestSphericalCoordinates()
        {
            try
            {
                // 测试笛卡尔到球坐标
                Vector3 northPole = Vector3.up;
                Vector2 spherical = SphericalCoordinates.FromCartesian(northPole);

                if (Mathf.Abs(spherical.y - Mathf.PI / 2f) > 0.0001f)
                    return false;

                // 测试球坐标到笛卡尔
                Vector2 testCoords = new Vector2(0f, 0f);
                Vector3 cartesian = SphericalCoordinates.ToCartesian(testCoords);

                if (Mathf.Abs(cartesian.z - 1f) > 0.0001f)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TestPositionCalculation()
        {
            try
            {
                var calculator = new SphericalPositionCalculator
                {
                    SphereCenter = Vector3.zero,
                    SphereRadius = 5f
                };

                // 测试北极位置
                Vector2 northPole = new Vector2(0f, Mathf.PI / 2f);
                Vector3 position = calculator.CalculatePosition(northPole);

                if (Mathf.Abs(position.y - 5f) > 0.0001f)
                    return false;

                // 验证位置在球面上
                if (Mathf.Abs(position.magnitude - 5f) > 0.0001f)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TestInputHandling()
        {
            try
            {
                var mockInput = new MockInputProvider();
                var handler = new MovementInputHandler(mockInput)
                {
                    MoveSpeed = 90f
                };

                // 测试无输入
                mockInput.Clear();
                Vector2 delta = handler.ProcessInput(1f);
                if (delta != Vector2.zero)
                    return false;

                // 测试右移
                mockInput.SetHorizontal(1f);
                delta = handler.ProcessInput(1f);
                if (delta.x <= 0)
                    return false;

                // 测试上移
                mockInput.Clear();
                mockInput.SetVertical(1f);
                delta = handler.ProcessInput(1f);
                if (delta.y <= 0)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TestSmoothMovement()
        {
            try
            {
                var controller = new SmoothMovementController
                {
                    UseSmoothMovement = true,
                    SmoothTime = 0.1f
                };

                Vector2 current = Vector2.zero;
                Vector2 target = new Vector2(10f, 0f);

                // 测试平滑移动
                Vector2 result = controller.SmoothMove(current, target);

                // 应该向目标移动
                if (result.x <= 0 || result.x >= 10f)
                    return false;

                // 多次调用应该接近目标
                for (int i = 0; i < 100; i++)
                {
                    current = controller.SmoothMove(current, target);
                }

                if (Mathf.Abs(current.x - 10f) > 0.01f)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
