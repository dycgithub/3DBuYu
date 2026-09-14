using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using _Project.Tests.Juice;

namespace _Project.Tests.Juice.Editor
{
    /// <summary>
    /// 使用 Unity Editor API 创建独立 Juice 验证场景，不修改 Build Settings。
    /// </summary>
    public static class JuiceSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Tests/Juice/JuiceLab.unity";
        private const string UxmlPath = "Assets/_Project/Tests/Juice/JuiceControls.uxml";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";
        private const string CentralPath = "Assets/_Project/SO/CentralBase.asset";

        /// <summary>
        /// 创建并保存 CombatLab Juice 测试场景。
        /// </summary>
        [MenuItem("Tools/Tests/Juice/Create Juice Lab")]
        public static void CreateScene()
        {
            Scene originalScene = SceneManager.GetActiveScene();
            PanelSettings panelSettings = LoadAsset<PanelSettings>(PanelSettingsPath);
            VisualTreeAsset visualTree = LoadAsset<VisualTreeAsset>(UxmlPath);
            Play.CentralSO centralConfig = LoadAsset<Play.CentralSO>(CentralPath);
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);

            try
            {
                SceneManager.SetActiveScene(scene);

                GameObject scopeObject = new("CombatLabLifetimeScope");
                CombatLabLifetimeScope scope =
                    scopeObject.AddComponent<CombatLabLifetimeScope>();

                GameObject compositionObject = new("CombatLabComposition");
                CombatLabComposition composition =
                    compositionObject.AddComponent<CombatLabComposition>();

                GameObject centralObject = new("CentralCore");
                Play.CentralCore centralCore = centralObject.AddComponent<Play.CentralCore>();

                GameObject targetObject = new("JuicePresentationTarget");
                targetObject.SetActive(false);
                JuicePresentationTarget target =
                    targetObject.AddComponent<JuicePresentationTarget>();
                GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visualObject.name = "JuiceVisual";
                visualObject.transform.SetParent(targetObject.transform, false);
                visualObject.transform.localScale = Vector3.one * 2f;
                Object.DestroyImmediate(visualObject.GetComponent<Collider>());

                GameObject cameraObject = new("Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 0f, -8f);
                camera.transform.LookAt(target.transform.position);
                camera.fieldOfView = 35f;

                GameObject lightObject = new("KeyLight");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
                light.intensity = 1.2f;

                UIDocument document = target.GetComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = visualTree;

                SetObjectField(scope, "_composition", composition);
                SetObjectField(composition, "_camera", camera);
                SetObjectField(composition, "_centralCore", centralCore);
                SetObjectField(centralCore, "centralSo", centralConfig);
                SetObjectField(target, "_visualRoot", visualObject.transform);
                SetObjectField(target, "_uiDocument", document);
                SetObjectArray(scope, "autoInjectGameObjects", targetObject);
                targetObject.SetActive(true);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (originalScene.IsValid() && originalScene.isLoaded)
                {
                    SceneManager.SetActiveScene(originalScene);
                }

                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// 加载 Builder 必需的资源，缺失时立即中止场景生成。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="path">项目内资源路径。</param>
        /// <returns>已加载的资源。</returns>
        private static T LoadAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new System.InvalidOperationException(
                    $"Juice 场景所需资源不存在或类型错误：{path}");
            }

            return asset;
        }

        /// <summary>
        /// 设置场景组件的对象引用并保存为场景本地序列化数据。
        /// </summary>
        /// <param name="target">要写入的组件。</param>
        /// <param name="fieldName">目标 SerializedProperty 名称。</param>
        /// <param name="value">要写入的场景对象。</param>
        private static void SetObjectField(
            Object target,
            string fieldName,
            Object value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                throw new System.InvalidOperationException(
                    $"未找到场景字段 {target.GetType().Name}.{fieldName}。");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 将一个场景对象加入 Scope 的自动注入列表。
        /// </summary>
        /// <param name="target">持有自动注入列表的 Scope。</param>
        /// <param name="fieldName">目标数组 SerializedProperty 名称。</param>
        /// <param name="value">需要被注入的场景对象。</param>
        private static void SetObjectArray(
            Object target,
            string fieldName,
            GameObject value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                throw new System.InvalidOperationException(
                    $"未找到场景数组 {target.GetType().Name}.{fieldName}。");
            }

            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
