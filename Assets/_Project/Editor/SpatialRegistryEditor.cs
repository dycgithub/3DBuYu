using UnityEditor;
using UnityEngine;
using SpatialSystem.Bridge;

/// <summary>
/// SpatialRegistry 自定义 Inspector — 运行时查询测试。
/// </summary>
[CustomEditor(typeof(SpatialRegistry))]
public class SpatialRegistryEditor : Editor
{
    #region Serialized Properties

    private SerializedProperty cellSizeProp;
    private SerializedProperty gridCenterProp;
    private SerializedProperty gridDimensionsProp;
    private SerializedProperty maxEntriesProp;
    private SerializedProperty queryBufferSizeProp;

    #endregion

    #region Query Test State

    private float testRadius = 10f;
    private int testLayerMask = SpatialRegistry.LAYER_ENEMY;
    private bool persistentQueryPreview;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        cellSizeProp         = serializedObject.FindProperty("cellSize");
        gridCenterProp       = serializedObject.FindProperty("gridCenter");
        gridDimensionsProp   = serializedObject.FindProperty("gridDimensions");
        maxEntriesProp       = serializedObject.FindProperty("maxEntries");
        queryBufferSizeProp  = serializedObject.FindProperty("queryBufferSize");
    }

    #endregion

    #region Inspector GUI

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── Grid Configuration ──
        EditorGUILayout.LabelField("── 网格配置 ──", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cellSizeProp);
        EditorGUILayout.PropertyField(gridCenterProp);
        EditorGUILayout.PropertyField(gridDimensionsProp);

        int totalCells = gridDimensionsProp.vector3IntValue.x *
                         gridDimensionsProp.vector3IntValue.y *
                         gridDimensionsProp.vector3IntValue.z;
        float worldSizeX = gridDimensionsProp.vector3IntValue.x * cellSizeProp.floatValue;
        float worldSizeY = gridDimensionsProp.vector3IntValue.y * cellSizeProp.floatValue;
        float worldSizeZ = gridDimensionsProp.vector3IntValue.z * cellSizeProp.floatValue;
        EditorGUILayout.LabelField(
            $"  总单元格: {totalCells:N0}   |   世界尺寸: {worldSizeX:F0} × {worldSizeY:F0} × {worldSizeZ:F0}",
            EditorStyles.miniLabel);

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(maxEntriesProp);
        EditorGUILayout.PropertyField(queryBufferSizeProp);

        serializedObject.ApplyModifiedProperties();

        // ── Runtime Stats ──
        var registry = (SpatialRegistry)target;

        if (Application.isPlaying)
        {
            DrawRuntimeStats(registry);
        }
    }

    #endregion

    #region Runtime Stats

    private void DrawRuntimeStats(SpatialRegistry registry)
    {
        EditorGUILayout.Space();

        // ── Query Test ──
        EditorGUILayout.LabelField("查询测试", EditorStyles.boldLabel);

        testRadius = EditorGUILayout.FloatField("  测试半径", testRadius);
        testLayerMask = EditorGUILayout.IntField("  层掩码", testLayerMask);
        EditorGUILayout.LabelField(
            "    LAYER_BULLET=1  |  LAYER_ENEMY=2  |  LAYER_PLAYER=4  |  LAYER_PICKUP=8",
            EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("执行查询"))
        {
            RunQueryTest(registry);
        }
        EditorGUILayout.EndHorizontal();

        // ── Quick Layer Tests ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("快捷查询", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("查询敌人",  GUILayout.Height(20))) QuickQuery(registry, SpatialRegistry.LAYER_ENEMY);
        if (GUILayout.Button("查询子弹",  GUILayout.Height(20))) QuickQuery(registry, SpatialRegistry.LAYER_BULLET);
        if (GUILayout.Button("查询玩家",  GUILayout.Height(20))) QuickQuery(registry, SpatialRegistry.LAYER_PLAYER);
        if (GUILayout.Button("查询掉落",  GUILayout.Height(20))) QuickQuery(registry, SpatialRegistry.LAYER_PICKUP);
        EditorGUILayout.EndHorizontal();
    }

    private void RunQueryTest(SpatialRegistry registry)
    {
        var sceneCamera = SceneView.lastActiveSceneView?.camera;
        if (sceneCamera == null)
        {
            Debug.LogWarning("[SpatialRegistryEditor] 未找到 Scene View 相机。");
            return;
        }

        Vector3 center = sceneCamera.transform.position;
        var results = registry.QueryRadiusManaged(center, testRadius, testLayerMask);
        Debug.Log($"[SpatialRegistry] 查询: center={center}, radius={testRadius}, mask={testLayerMask}, results={results.Count}");
        foreach (var entity in results)
        {
            Debug.Log($"  - {entity.Transform?.name} @ {entity.Position}, layer={entity.Transform?.gameObject.layer}, alive={entity.IsAlive}");
        }
    }

    private void QuickQuery(SpatialRegistry registry, int layerMask)
    {
        testLayerMask = layerMask;
        RunQueryTest(registry);
    }

    #endregion
}
