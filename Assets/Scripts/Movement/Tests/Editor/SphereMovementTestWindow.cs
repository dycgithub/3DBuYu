using UnityEditor;
using UnityEngine;

namespace SphereMovement.Tests.Editor
{
    /// <summary>
    /// 球面移动系统测试窗口
    /// 提供图形界面来运行测试和查看结果
    /// </summary>
    public class SphereMovementTestWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private bool _showUnitTests = true;
        private bool _showIntegrationTests = true;
        private bool _showPerformanceTests = false;

        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _boxStyle;

        [MenuItem("Window/Sphere Movement/测试窗口")]
        public static void ShowWindow()
        {
            var window = GetWindow<SphereMovementTestWindow>("球面移动测试");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        [MenuItem("Window/Sphere Movement/打开测试运行器 %#t")]
        public static void OpenTestRunner()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
        }

        private void OnEnable()
        {
            InitializeStyles();
        }

        private void InitializeStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(10, 10, 10, 10)
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(15, 15, 10, 10),
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            };
        }

        private void OnGUI()
        {
            DrawHeader();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawQuickActions();
            DrawTestCategories();
            DrawDocumentation();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("球面移动系统测试中心", _headerStyle);
            EditorGUILayout.LabelField("运行单元测试、集成测试和性能测试", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(10);
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.LabelField("快速操作", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("运行所有测试", _buttonStyle, GUILayout.Height(40)))
            {
                RunAllTests();
            }

            if (GUILayout.Button("打开测试运行器", _buttonStyle, GUILayout.Height(40)))
            {
                OpenTestRunner();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        private void DrawTestCategories()
        {
            EditorGUILayout.LabelField("测试类别", EditorStyles.boldLabel);

            // 单元测试
            _showUnitTests = EditorGUILayout.Foldout(_showUnitTests, "单元测试", true);
            if (_showUnitTests)
            {
                EditorGUILayout.BeginVertical(_boxStyle);

                EditorGUILayout.LabelField("坐标转换测试", EditorStyles.boldLabel);
                if (GUILayout.Button("运行 SphericalCoordinatesTests"))
                {
                    RunTest("SphericalCoordinatesTests");
                }

                EditorGUILayout.LabelField("输入处理测试", EditorStyles.boldLabel);
                if (GUILayout.Button("运行 MovementInputHandlerTests"))
                {
                    RunTest("MovementInputHandlerTests");
                }

                EditorGUILayout.LabelField("平滑移动测试", EditorStyles.boldLabel);
                if (GUILayout.Button("运行 SmoothMovementControllerTests"))
                {
                    RunTest("SmoothMovementControllerTests");
                }

                EditorGUILayout.LabelField("位置计算测试", EditorStyles.boldLabel);
                if (GUILayout.Button("运行 SphericalPositionCalculatorTests"))
                {
                    RunTest("SphericalPositionCalculatorTests");
                }

                EditorGUILayout.LabelField("朝向控制测试", EditorStyles.boldLabel);
                if (GUILayout.Button("运行 OrientationControllerTests"))
                {
                    RunTest("OrientationControllerTests");
                }

                EditorGUILayout.LabelField("模拟输入测试", EditorStyles.boldLabel);
                if (GUILayout.Button("运行 MockInputProviderTests"))
                {
                    RunTest("MockInputProviderTests");
                }

                EditorGUILayout.EndVertical();
            }

            // 集成测试
            _showIntegrationTests = EditorGUILayout.Foldout(_showIntegrationTests, "集成测试", true);
            if (_showIntegrationTests)
            {
                EditorGUILayout.BeginVertical(_boxStyle);

                EditorGUILayout.LabelField("系统集成测试", EditorStyles.boldLabel);
                if (GUILayout.Button("运行 SphericalMovementIntegrationTests"))
                {
                    RunTest("SphericalMovementIntegrationTests");
                }

                EditorGUILayout.EndVertical();
            }

            GUILayout.Space(10);
        }

        private void DrawDocumentation()
        {
            EditorGUILayout.LabelField("文档与帮助", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(_boxStyle);

            EditorGUILayout.LabelField("接口实现示例", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "// 自定义输入提供器\n" +
                "var customInput = new MyCustomInputProvider();\n" +
                "movement.InputHandler = new MovementInputHandler(customInput);",
                MessageType.Info);

            GUILayout.Space(10);

            EditorGUILayout.LabelField("可用接口", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• IInputProvider - 输入提供", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• IMovementInputHandler - 输入处理", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• ISphericalPositionCalculator - 位置计算", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• ISmoothMovementController - 平滑移动", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• IOrientationController - 朝向控制", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void RunAllTests()
        {
            Debug.Log("运行所有球面移动系统测试...");
            OpenTestRunner();
        }

        private void RunTest(string testClassName)
        {
            Debug.Log($"运行测试: {testClassName}");
            OpenTestRunner();
        }
    }
}
