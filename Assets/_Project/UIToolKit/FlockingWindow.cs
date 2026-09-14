#if UNITY_EDITOR
using FlockingSystem.ECS;
using Services;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VContainer;

/// <summary>
/// 提供 Flocking 的 Editor 调试窗口。
/// 窗口显示实际 ECS 活跃代理数和场上存活敌人数，并按绘制开关
/// 将 EnemyFlockSettingsSO 中的世界游动范围绘制到 Scene 视图。
/// </summary>
public class FlockingWindow : EditorWindow
{
    private const string DefaultSettingsPath =
        "Assets/_Project/SO/Flocking/EnemyFlockSettings.asset";
    private const double RefreshIntervalSeconds = 0.1d;

    private static readonly Color SwimBoundsColor = new(0f, 0.85f, 1f, 0.8f);

    [SerializeField] private EnemyFlockSettingsSO _flockSettings;
    [SerializeField] private bool _drawSwimBounds = true;

    private ObjectField _settingsField;
    private Label _stateLabel;
    private Label _flockingCountLabel;
    private Label _aliveCountLabel;
    private Label _centerLabel;
    private Label _sizeLabel;
    private Toggle _drawSwimBoundsToggle;

    // 面板读取数量服务的实际存活数，不参与生成和补怪调度。
    private IEnemyPopulationService _populationService;
    private GameLoopLifetimeScope _gameLoopScope;
    private double _nextRefreshTime;
    private int _flockingCount = -1;
    private int _aliveCount = -1;

    /// <summary>
    /// 打开 Flocking 调试窗口。
    /// </summary>
    [MenuItem("Window/UI Toolkit/FlockingWindow")]
    public static void ShowExample()
    {
        FlockingWindow window = GetWindow<FlockingWindow>();
        window.titleContent = new GUIContent("Flocking 调试");
        window.minSize = new Vector2(360f, 220f);
    }

    /// <summary>
    /// 创建窗口控件，并绑定配置选择和运行时数据标签。
    /// </summary>
    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.Clear();
        root.style.paddingLeft = 8f;
        root.style.paddingRight = 8f;
        root.style.paddingTop = 8f;
        root.style.paddingBottom = 8f;

        Label title = new("Flocking 调试")
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 8f
            }
        };
        root.Add(title);

        _settingsField = new ObjectField("Flocking 配置")
        {
            objectType = typeof(EnemyFlockSettingsSO),
            allowSceneObjects = false,
            value = _flockSettings
        };
        _settingsField.RegisterValueChangedCallback(HandleSettingsChanged);
        root.Add(_settingsField);

        root.Add(new Label("运行时数量")
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginTop = 8f
            }
        });
        _flockingCountLabel = new Label();
        _aliveCountLabel = new Label();
        root.Add(_flockingCountLabel);
        root.Add(_aliveCountLabel);

        root.Add(new Label("游动范围")
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginTop = 8f
            }
        });
        _centerLabel = new Label();
        _sizeLabel = new Label();
        _drawSwimBoundsToggle = new Toggle("绘制游泳范围")
        {
            value = _drawSwimBounds
        };
        _drawSwimBoundsToggle.RegisterValueChangedCallback(HandleDrawSwimBoundsChanged);
        root.Add(_drawSwimBoundsToggle);
        root.Add(_centerLabel);
        root.Add(_sizeLabel);

        _stateLabel = new Label
        {
            style =
            {
                marginTop = 8f,
                whiteSpace = WhiteSpace.Normal
            }
        };
        root.Add(_stateLabel);

        RefreshLabels();
    }

    private void OnEnable()
    {
        _flockSettings = GetSceneSettings();
        EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        SceneView.duringSceneGui += DrawSwimBounds;
        _nextRefreshTime = 0d;
        RefreshRuntimeData();
    }

    private void OnDisable()
    {
        EditorSceneManager.activeSceneChangedInEditMode -= HandleActiveSceneChanged;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        SceneView.duringSceneGui -= DrawSwimBounds;
        _populationService = null;
        _gameLoopScope = null;
        SceneView.RepaintAll();
    }

    private void Update()
    {
        if (EditorApplication.timeSinceStartup < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
        RefreshRuntimeData();
        RefreshLabels();
    }

    private void HandleSettingsChanged(ChangeEvent<Object> changeEvent)
    {
        _flockSettings = changeEvent.newValue as EnemyFlockSettingsSO;
        RefreshLabels();
        SceneView.RepaintAll();
    }

    private void HandleDrawSwimBoundsChanged(ChangeEvent<bool> changeEvent)
    {
        _drawSwimBounds = changeEvent.newValue;
        RefreshLabels();
        SceneView.RepaintAll();
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        _populationService = null;
        _gameLoopScope = null;
        _flockSettings = GetSceneSettings();
        RefreshLabels();
        SceneView.RepaintAll();
    }

    private void HandlePlayModeChanged(PlayModeStateChange state)
    {
        _populationService = null;
        _gameLoopScope = null;
        _nextRefreshTime = 0d;
        RefreshRuntimeData();
        RefreshLabels();
        SceneView.RepaintAll();
    }

    private void RefreshRuntimeData()
    {
        if (!Application.isPlaying)
        {
            _flockingCount = -1;
            _aliveCount = -1;
            return;
        }

        _flockingCount = CountActiveAgents(World.DefaultGameObjectInjectionWorld);
        ResolvePopulationService();
        _aliveCount = _populationService != null
            ? Mathf.Max(0, _populationService.EnemiesAlive)
            : -1;
    }

    private void ResolvePopulationService()
    {
        if (_gameLoopScope != null && _gameLoopScope.Container != null && _populationService != null)
        {
            return;
        }

        _gameLoopScope = Object.FindFirstObjectByType<GameLoopLifetimeScope>();
        _populationService = null;
        if (_gameLoopScope != null && _gameLoopScope.Container != null)
        {
            _gameLoopScope.Container.TryResolve(out _populationService);
        }
    }

    private void RefreshLabels()
    {
        if (_settingsField != null && _settingsField.value != _flockSettings)
        {
            _settingsField.SetValueWithoutNotify(_flockSettings);
        }

        if (_drawSwimBoundsToggle != null && _drawSwimBoundsToggle.value != _drawSwimBounds)
        {
            _drawSwimBoundsToggle.SetValueWithoutNotify(_drawSwimBounds);
        }

        if (_flockingCountLabel == null)
        {
            return;
        }

        _flockingCountLabel.text = $"ECS Flocking：{FormatCount(_flockingCount)}";
        _aliveCountLabel.text = $"场上存活：{FormatCount(_aliveCount)}";

        if (_flockSettings == null)
        {
            _centerLabel.text = "中心：不可用";
            _sizeLabel.text = "尺寸：不可用";
        }
        else
        {
            Bounds bounds = CalculateSwimBounds(
                _flockSettings.SwimCenter,
                _flockSettings.SwimLimits);
            _centerLabel.text = $"中心：{FormatVector(bounds.center)}";
            _sizeLabel.text = $"尺寸：{FormatVector(bounds.size)}";
        }

        _stateLabel.text = GetStateMessage();
    }

    private string GetStateMessage()
    {
        if (_flockSettings == null)
        {
            return Application.isPlaying
                ? "状态：运行中；未找到 Flocking 配置，范围绘制不可用。"
                : "状态：未运行；未找到 Flocking 配置，范围绘制不可用。";
        }

        if (!Application.isPlaying)
        {
            return _drawSwimBounds
                ? "状态：未运行；Scene 视图中的范围框来自当前配置。"
                : "状态：未运行；Scene 视图范围绘制已关闭。";
        }

        if (_flockingCount < 0)
        {
            return "状态：ECS World 不可用，等待运行时 World 创建。";
        }

        if (_aliveCount < 0)
        {
            return "状态：GameLoop Scope 尚未准备好，等待存活数量服务。";
        }

        return "状态：运行中；数量按刷新间隔更新。";
    }

    private void DrawSwimBounds(SceneView sceneView)
    {
        if (!ShouldDrawSwimBounds())
        {
            return;
        }

        Bounds bounds = CalculateSwimBounds(
            _flockSettings.SwimCenter,
            _flockSettings.SwimLimits);
        Color previousColor = Handles.color;
        Handles.color = SwimBoundsColor;
        Handles.DrawWireCube(bounds.center, bounds.size);
        Handles.color = previousColor;
    }

    private bool ShouldDrawSwimBounds()
    {
        return _drawSwimBounds && _flockSettings != null;
    }

    private static EnemyFlockSettingsSO GetSceneSettings()
    {
        GameLoopLifetimeScope scope = Object.FindFirstObjectByType<GameLoopLifetimeScope>();
        if (scope != null)
        {
            SerializedObject serializedScope = new(scope);
            SerializedProperty settingsProperty = serializedScope.FindProperty("enemyFlockSettings");
            EnemyFlockSettingsSO sceneSettings = settingsProperty != null
                ? settingsProperty.objectReferenceValue as EnemyFlockSettingsSO
                : null;
            if (sceneSettings != null)
            {
                return sceneSettings;
            }
        }

        return AssetDatabase.LoadAssetAtPath<EnemyFlockSettingsSO>(DefaultSettingsPath);
    }

    private static int CountActiveAgents(World world)
    {
        if (world == null || !world.IsCreated)
        {
            return -1;
        }

        EntityManager entityManager = world.EntityManager;
        entityManager.CompleteAllTrackedJobs();
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<EnemyFlockAgent>(),
            ComponentType.ReadOnly<EnemyFlockActive>());
        return query.CalculateEntityCount();
    }

    private static Bounds CalculateSwimBounds(Vector3 center, Vector3 halfExtents)
    {
        return new Bounds(center, halfExtents * 2f);
    }

    private static string FormatCount(int count)
    {
        return count >= 0 ? count.ToString() : "不可用";
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F1}, {value.y:F1}, {value.z:F1})";
    }
}
#endif
