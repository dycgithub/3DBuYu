using CombatSystem;
using EffectSystem;
using EnemySystem;
using EnemySystem.Components;
using FlockingSystem.ECS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using _Project.Tests.CombatPlayground.Implement;
using _Project.Tests.CombatPlayground.View;

namespace _Project.Tests.CombatPlayground.Editor
{
    /// <summary>
    /// 通过 Unity Editor API 创建独立 CombatPlayground 场景，不修改正式场景和构建设置。
    /// </summary>
    public static class CombatPlaygroundSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Tests/CombatPlayground/CombatPlayground.unity";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";
        private const string UxmlPath = "Assets/_Project/Tests/CombatPlayground/View/CombatPlayground.uxml";
        private const string TransmitterPath = "Assets/_Project/SO/Transmitter/PortConfig_Top.asset";
        private const string DefaultProjectilePath = "Assets/_Project/SO/Bullet/DefaultBulletProfile.asset";
        private const string EnemyPath = "Assets/_Project/Prefabs/Enemy/EnemyNormal.prefab";
        private const string BuffPath = "Assets/_Project/SO/Enemy/EnemySlowBuff.asset";
        private const string EffectCatalogPath = "Assets/_Project/SO/Effects/CombatEffectCatalog.asset";
        private const string FlockSettingsPath = "Assets/_Project/SO/Flocking/EnemyFlockSettings.asset";

        /// <summary>创建并保存独立实验场景。</summary>
        [MenuItem("Tools/Tests/Create Combat Playground")]
        public static void BuildScene()
        {
            Scene originalScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);

            try
            {
                SceneManager.SetActiveScene(scene);
                Camera sceneCamera = CreateCamera();
                CreateLight();
                CreateFloor();
                EffectManager effectManager = CreateEffectManager();
                GameObject emitter = CreateEmitter();
                Enemy target = CreateTarget();
                Transform firePoint = CreateFirePoint(emitter.transform);
                CombatPlaygroundJuice juice = CreateJuice(target, emitter.transform);
                CombatPlaygroundView view = CreateUi(out UIDocument document);
                CombatPlaygroundController controller = CreateController(
                    sceneCamera,
                    target,
                    firePoint,
                    juice,
                    document);
                SetObjectField(view, "controller", controller);
                CombatPlaygroundComposition composition = CreateComposition(
                    sceneCamera,
                    target,
                    firePoint,
                    controller,
                    view,
                    juice,
                    effectManager);
                CombatPlaygroundLifetimeScope scope = CreateScope(composition);
                SetScopeAssets(scope);
                SetAutoInjectObjects(
                    scope,
                    controller.gameObject,
                    view.gameObject,
                    target.gameObject,
                    effectManager.gameObject);

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

        /// <summary>创建可漫游的主相机。</summary>
        private static Camera CreateCamera()
        {
            GameObject cameraObject = new("Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 5f, -12f);
            camera.transform.rotation = Quaternion.Euler(17f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.07f, 1f);
            cameraObject.AddComponent<CombatPlaygroundFreeCamera>();
            return camera;
        }

        /// <summary>创建测试场景的主方向光。</summary>
        private static Light CreateLight()
        {
            GameObject lightObject = new("KeyLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            return light;
        }

        /// <summary>创建不依赖正式资产的地面。</summary>
        private static GameObject CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "PlaygroundFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 4f);
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            return floor;
        }

        /// <summary>创建投射物和敌人表现使用的特效管理器。</summary>
        private static EffectManager CreateEffectManager()
        {
            GameObject effectObject = new("EffectManager");
            return effectObject.AddComponent<EffectManager>();
        }

        /// <summary>实例化一个正式发射器模型作为独立实验对象。</summary>
        private static GameObject CreateEmitter()
        {
            GameObject prefab = LoadAsset<GameObject>("Assets/_Project/Prefabs/Player/Transmitter.prefab");
            GameObject emitter = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            emitter.name = "CombatPlaygroundEmitter";
            emitter.transform.position = Vector3.zero;
            emitter.transform.rotation = Quaternion.identity;
            return emitter;
        }

        /// <summary>实例化一个正式 Normal 敌人作为唯一目标。</summary>
        private static Enemy CreateTarget()
        {
            Enemy prefab = LoadAsset<Enemy>(EnemyPath);
            Enemy target = (Enemy)PrefabUtility.InstantiatePrefab(prefab);
            target.name = "CombatPlaygroundTarget";
            target.transform.position = new Vector3(0f, 0f, 7f);
            RemoveComponent<EnemyDeathRewardComponent>(target.gameObject);
            RemoveComponent<EnemyContactDamageComponent>(target.gameObject);
            return target;
        }

        /// <summary>移除不属于单敌人实验边界的正式场景组件。</summary>
        private static void RemoveComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null)
            {
                Object.DestroyImmediate(component);
            }
        }

        /// <summary>在发射器前方创建固定发射点。</summary>
        private static Transform CreateFirePoint(Transform emitter)
        {
            GameObject firePointObject = new("FirePoint");
            firePointObject.transform.SetParent(emitter, false);
            firePointObject.transform.localPosition = new Vector3(0f, 0f, 1.8f);
            firePointObject.transform.localRotation = Quaternion.identity;
            return firePointObject.transform;
        }

        /// <summary>创建场景专用 Juice 组件。</summary>
        private static CombatPlaygroundJuice CreateJuice(Enemy target, Transform emitter)
        {
            CombatPlaygroundJuice juice = target.gameObject.AddComponent<CombatPlaygroundJuice>();
            SetObjectField(juice, "target", target);
            SetObjectField(juice, "targetVisual", target.transform);
            SetObjectField(juice, "emitterVisual", emitter);
            return juice;
        }

        /// <summary>创建并绑定静态 UI Toolkit 文档。</summary>
        private static CombatPlaygroundView CreateUi(out UIDocument document)
        {
            GameObject uiObject = new("CombatPlaygroundUI");
            document = uiObject.AddComponent<UIDocument>();
            CombatPlaygroundView view = uiObject.AddComponent<CombatPlaygroundView>();
            document.panelSettings = LoadAsset<PanelSettings>(PanelSettingsPath);
            document.visualTreeAsset = LoadAsset<VisualTreeAsset>(UxmlPath);
            return view;
        }

        /// <summary>创建场景控制器并设置当前实验资产列表。</summary>
        private static CombatPlaygroundController CreateController(
            Camera sceneCamera,
            Enemy target,
            Transform firePoint,
            CombatPlaygroundJuice juice,
            UIDocument document)
        {
            GameObject controllerObject = new("CombatPlaygroundController");
            CombatPlaygroundController controller = controllerObject.AddComponent<CombatPlaygroundController>();
            SetObjectField(controller, "sceneCamera", sceneCamera);
            SetObjectField(controller, "target", target);
            SetObjectField(controller, "firePoint", firePoint);
            SetObjectField(controller, "transmitter", LoadAsset<Play.TransmitterSO>(TransmitterPath));
            SetObjectField(controller, "defaultProjectile", LoadAsset<ProjectileProfileSO>(DefaultProjectilePath));
            SetObjectArray(controller, "projectileProfiles", new Object[]
            {
                LoadAsset<ProjectileProfileSO>(DefaultProjectilePath),
                LoadAsset<ProjectileProfileSO>("Assets/_Project/SO/Item/Projectile/ProjectileProfile_003.asset"),
                LoadAsset<ProjectileProfileSO>("Assets/_Project/SO/Item/Projectile/ProjectileProfile_007.asset"),
                LoadAsset<ProjectileProfileSO>("Assets/_Project/SO/Item/Projectile/ProjectileProfile_010.asset"),
                LoadAsset<ProjectileProfileSO>("Assets/_Project/SO/Item/Projectile/ProjectileProfile_011.asset"),
                LoadAsset<ProjectileProfileSO>("Assets/_Project/SO/Item/Projectile/ProjectileProfile_013.asset"),
                LoadAsset<ProjectileProfileSO>("Assets/_Project/SO/Item/Projectile/ProjectileProfile_014.asset")
            });
            SetObjectArray(controller, "skills", new Object[]
            {
                LoadAsset<SkillDefinitionSO>("Assets/_Project/SO/Item/Central/SkillDefinition_001.asset"),
                LoadAsset<SkillDefinitionSO>("Assets/_Project/SO/Item/Central/SkillDefinition_010.asset"),
                LoadAsset<SkillDefinitionSO>("Assets/_Project/SO/Item/Central/SkillDefinition_012.asset"),
                LoadAsset<SkillDefinitionSO>("Assets/_Project/SO/Item/Central/SkillDefinition_013.asset"),
                LoadAsset<SkillDefinitionSO>("Assets/_Project/SO/Item/Central/SkillDefinition_015.asset")
            });
            SetObjectField(controller, "buffDefinition", LoadAsset<BuffDefinitionSO>(BuffPath));
            SetObjectField(controller, "juice", juice);
            return controller;
        }

        /// <summary>创建并配置场景组件组合。</summary>
        private static CombatPlaygroundComposition CreateComposition(
            Camera sceneCamera,
            Enemy target,
            Transform firePoint,
            CombatPlaygroundController controller,
            CombatPlaygroundView view,
            CombatPlaygroundJuice juice,
            EffectManager effectManager)
        {
            GameObject compositionObject = new("CombatPlaygroundComposition");
            CombatPlaygroundComposition composition =
                compositionObject.AddComponent<CombatPlaygroundComposition>();
            SetObjectField(composition, "sceneCamera", sceneCamera);
            SetObjectField(composition, "target", target);
            SetObjectField(composition, "firePoint", firePoint);
            SetObjectField(composition, "transmitter", LoadAsset<Play.TransmitterSO>(TransmitterPath));
            SetObjectField(composition, "defaultProjectile", LoadAsset<ProjectileProfileSO>(DefaultProjectilePath));
            SetObjectField(composition, "buffDefinition", LoadAsset<BuffDefinitionSO>(BuffPath));
            SetObjectField(composition, "effectManager", effectManager);
            SetObjectField(composition, "controller", controller);
            SetObjectField(composition, "view", view);
            SetObjectField(composition, "juice", juice);
            return composition;
        }

        /// <summary>创建场景作用域。</summary>
        private static CombatPlaygroundLifetimeScope CreateScope(
            CombatPlaygroundComposition composition)
        {
            GameObject scopeObject = new("CombatPlaygroundLifetimeScope");
            CombatPlaygroundLifetimeScope scope =
                scopeObject.AddComponent<CombatPlaygroundLifetimeScope>();
            SetObjectField(scope, "composition", composition);
            return scope;
        }

        /// <summary>设置独立作用域使用的正式目录配置。</summary>
        private static void SetScopeAssets(CombatPlaygroundLifetimeScope scope)
        {
            SetObjectField(scope, "combatEffectCatalog", LoadAsset<CombatEffectCatalogSO>(EffectCatalogPath));
            SetObjectField(scope, "enemyFlockSettings", LoadAsset<EnemyFlockSettingsSO>(FlockSettingsPath));
        }

        /// <summary>把需要注入的场景对象加入独立作用域列表。</summary>
        private static void SetAutoInjectObjects(CombatPlaygroundLifetimeScope scope, params GameObject[] objects)
        {
            SetObjectArray(scope, "autoInjectGameObjects", objects);
        }

        /// <summary>设置场景组件的对象引用。</summary>
        private static void SetObjectField(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                throw new System.InvalidOperationException(
                    $"未找到场景字段 {target.GetType().Name}.{fieldName}。");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>设置场景组件的对象数组引用。</summary>
        private static void SetObjectArray(Object target, string fieldName, Object[] values)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                throw new System.InvalidOperationException(
                    $"未找到场景数组 {target.GetType().Name}.{fieldName}。");
            }

            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>按路径加载并校验场景资源。</summary>
        private static T LoadAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new System.InvalidOperationException($"缺少 CombatPlayground 资源：{path}");
            }

            return asset;
        }
    }
}
