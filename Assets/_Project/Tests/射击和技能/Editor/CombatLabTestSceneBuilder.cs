using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// 通过 Unity Editor 创建射击和技能测试场景，避免手工维护场景序列化数据。
/// </summary>
public static class CombatLabTestSceneBuilder
{
    private const string ScenePath =
        "Assets/_Project/Tests/射击和技能/CombatLabTestScene.unity";

    /// <summary>创建并保存独立的 CombatLab 测试场景。</summary>
    [MenuItem("Tools/Tests/Create CombatLab Test Scene")]
    public static void BuildScene()
    {
        Scene originalScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Additive);

        try
        {
            SceneManager.SetActiveScene(scene);

            Camera camera = CreateCamera();
            Play.CentralCore centralCore = CreateCentralCore();
            CombatLabComposition composition = CreateComposition(camera, centralCore);
            CombatLabLifetimeScope scope = CreateScope(composition);
            EffectSystem.EffectManager effectManager = CreateEffectManager();
            SpatialSystem.Bridge.SpatialRegistry spatialRegistry = CreateSpatialRegistry();
            SetReference(composition, "_effectManager", effectManager);
            SetReference(composition, "_spatialRegistry", spatialRegistry);
            SetModules(composition);

            EnemySystem.Enemy target = CreateTarget();
            EnemySystem.Spawning.SpawnPositionProvider spawnPositionProvider =
                CreateSpawnPositionProvider();
            UIDocument uiDocument = CreateUiDocument();
            CombatLabTestDriver driver = CreateDriver(centralCore, target, uiDocument);
            AddAutoInjectedObjects(scope, driver.gameObject, target.gameObject);

            SetReference(scope, "_combatEffectCatalog", LoadAsset<EffectSystem.CombatEffectCatalogSO>(
                "Assets/_Project/SO/Effects/CombatEffectCatalog.asset"));
            SetReference(scope, "_enemyFlockSettings", LoadAsset<FlockingSystem.ECS.EnemyFlockSettingsSO>(
                "Assets/_Project/SO/Flocking/EnemyFlockSettings.asset"));
            SetReference(composition, "_enemy", target);
            SetReference(composition, "_spawnPositionProvider", spawnPositionProvider);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
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

    /// <summary>创建测试相机。</summary>
    private static Camera CreateCamera()
    {
        GameObject cameraObject = new("Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.transform.position = new Vector3(0f, 4f, -11f);
        camera.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.04f, 0.07f, 1f);
        return camera;
    }

    /// <summary>创建引用正式中央配置的中央核心。</summary>
    private static Play.CentralCore CreateCentralCore()
    {
        GameObject coreObject = new("CentralCore");
        Play.CentralCore centralCore = coreObject.AddComponent<Play.CentralCore>();
        SetReference(
            centralCore,
            "centralSo",
            LoadAsset<Play.CentralSO>("Assets/_Project/SO/CentralBase.asset"));
        return centralCore;
    }

    /// <summary>创建只配置必需对象的显式组合。</summary>
    private static CombatLabComposition CreateComposition(
        Camera camera,
        Play.CentralCore centralCore)
    {
        GameObject compositionObject = new("CombatLabComposition");
        CombatLabComposition composition =
            compositionObject.AddComponent<CombatLabComposition>();
        SetReference(composition, "_camera", camera);
        SetReference(composition, "_centralCore", centralCore);
        return composition;
    }

    /// <summary>创建使用默认父级解析的 CombatLab 作用域。</summary>
    private static CombatLabLifetimeScope CreateScope(CombatLabComposition composition)
    {
        GameObject scopeObject = new("CombatLabLifetimeScope");
        CombatLabLifetimeScope scope = scopeObject.AddComponent<CombatLabLifetimeScope>();
        SetReference(scope, "_composition", composition);
        return scope;
    }

    /// <summary>创建由 CombatLab 组合注入的特效管理器。</summary>
    private static EffectSystem.EffectManager CreateEffectManager()
    {
        GameObject effectObject = new("CombatLabEffects");
        return effectObject.AddComponent<EffectSystem.EffectManager>();
    }

    /// <summary>创建投射物效果和目标查询使用的空间注册表。</summary>
    private static SpatialSystem.Bridge.SpatialRegistry CreateSpatialRegistry()
    {
        GameObject spatialObject = new("CombatLabSpatialRegistry");
        return spatialObject.AddComponent<SpatialSystem.Bridge.SpatialRegistry>();
    }

    /// <summary>创建敌人生成服务需要的最小出生点提供器。</summary>
    private static EnemySystem.Spawning.SpawnPositionProvider CreateSpawnPositionProvider()
    {
        GameObject spawnObject = new("CombatLabSpawnPositionProvider");
        return spawnObject.AddComponent<EnemySystem.Spawning.SpawnPositionProvider>();
    }

    /// <summary>实例化一个正式敌人预制体作为射击和技能目标。</summary>
    private static EnemySystem.Enemy CreateTarget()
    {
        EnemySystem.Enemy prefab = LoadAsset<EnemySystem.Enemy>(
            "Assets/_Project/Prefabs/Enemy/EnemyNormal.prefab");
        EnemySystem.Enemy target = (EnemySystem.Enemy)PrefabUtility.InstantiatePrefab(prefab);
        target.name = "CombatLabTestTarget";
        target.transform.position = new Vector3(0f, 0f, 5f);
        return target;
    }

    /// <summary>创建引用场景 UXML 的 UI Toolkit 文档。</summary>
    private static UIDocument CreateUiDocument()
    {
        GameObject uiObject = new("CombatLabTestUI");
        UIDocument document = uiObject.AddComponent<UIDocument>();
        PanelSettings panelSettings = LoadAsset<PanelSettings>(
            "Assets/UI Toolkit/PanelSettings.asset");
        SetReference(document, "m_PanelSettings", panelSettings);
        document.visualTreeAsset = LoadAsset<VisualTreeAsset>(
            "Assets/_Project/Tests/射击和技能/CombatLabTestScene.uxml");
        return document;
    }

    /// <summary>创建场景操作驱动并设置所有场景引用。</summary>
    private static CombatLabTestDriver CreateDriver(
        Play.CentralCore centralCore,
        EnemySystem.Enemy target,
        UIDocument uiDocument)
    {
        GameObject driverObject = new("CombatLabTestDriver");
        CombatLabTestDriver driver = driverObject.AddComponent<CombatLabTestDriver>();
        SetReference(driver, "_centralCore", centralCore);
        SetReference(driver, "_target", target);
        SetReference(
            driver,
            "_skillDefinition",
            LoadAsset<CombatSystem.SkillDefinitionSO>(
                "Assets/_Project/SO/Item/Central/SkillDefinition_010.asset"));
        SetReference(driver, "_uiDocument", uiDocument);
        return driver;
    }

    /// <summary>启用射击、伤害、技能、特效、空间和敌人六个最小模块。</summary>
    private static void SetModules(CombatLabComposition composition)
    {
        SerializedObject serialized = new(composition);
        SerializedProperty modules = serialized.FindProperty("_modules");
        CombatLabComposition.CombatLabModuleType[] values =
        {
            CombatLabComposition.CombatLabModuleType.Projectile,
            CombatLabComposition.CombatLabModuleType.Damage,
            CombatLabComposition.CombatLabModuleType.Ability,
             CombatLabComposition.CombatLabModuleType.Effects,
             CombatLabComposition.CombatLabModuleType.Spatial,
             CombatLabComposition.CombatLabModuleType.Enemy
        };
        modules.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            modules.GetArrayElementAtIndex(index).enumValueIndex = (int)values[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>把场景驱动加入作用域的自动注入对象列表。</summary>
    private static void AddAutoInjectedObjects(
        CombatLabLifetimeScope scope,
        GameObject driver,
        GameObject target)
    {
        SerializedObject serialized = new(scope);
        SerializedProperty objects = serialized.FindProperty("autoInjectGameObjects");
        objects.arraySize = 2;
        objects.GetArrayElementAtIndex(0).objectReferenceValue = driver;
        objects.GetArrayElementAtIndex(1).objectReferenceValue = target;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>设置 MonoBehaviour 的序列化对象引用。</summary>
    private static void SetReference(
        Object target,
        string propertyName,
        Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>按路径加载场景需要的正式配置资产。</summary>
    private static T LoadAsset<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new System.InvalidOperationException($"CombatLab 测试场景缺少资产：{path}");
        }

        return asset;
    }
}
