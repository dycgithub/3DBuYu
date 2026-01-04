using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SphereMovement))]
public class SphereMovementEditor : Editor
{
    private SphereMovement _movement;
    private bool _showPreview = true;

    private void OnEnable()
    {
        _movement = target as SphereMovement;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 绘制目标物体字段
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetObject"),
            new GUIContent("目标物体", "要在球面上移动的物体"));

        // 绘制其他属性
        DrawPropertiesExcluding(serializedObject,
            "m_Script",
            "targetObject",
            "latitudeLines",
            "longitudeLines",
            "gridColor");

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("经纬线设置", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("latitudeLines"),
            new GUIContent("纬度线条数", "水平圆的个数"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("longitudeLines"),
            new GUIContent("经度线条数", "垂直圆的个数"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gridColor"),
            new GUIContent("经纬线颜色"));

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);

        // 调试信息
        _showPreview = EditorGUILayout.Foldout(_showPreview, "调试信息");
        if (_showPreview)
        {
            EditorGUI.indentLevel++;

            Transform targetObj = _movement.MovingObject;
            EditorGUILayout.ObjectField("当前目标物体", targetObj, typeof(Transform), true);

            Vector3 pos = _movement.CurrentPositionOnSphere;
            EditorGUILayout.Vector3Field("球面位置", pos);

            float lonDegrees = _movement.CurrentLongitude * Mathf.Rad2Deg;
            float latDegrees = _movement.CurrentLatitude * Mathf.Rad2Deg;
            EditorGUILayout.LabelField($"经度: {lonDegrees:F1}°");
            EditorGUILayout.LabelField($"纬度: {latDegrees:F1}°");

            Vector3 forward = _movement.GetForwardDirection();
            EditorGUILayout.Vector3Field("前进方向", forward);

            Vector3 longitudeTangent = _movement.GetLongitudeTangent();
            EditorGUILayout.Vector3Field("经线切线(南北)", longitudeTangent);

            Vector3 latitudeTangent = _movement.GetLatitudeTangent();
            EditorGUILayout.Vector3Field("纬线切线(东西)", latitudeTangent);

            EditorGUI.indentLevel--;
        }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
    private static void DrawGizmoForSphereMovement(SphereMovement movement, GizmoType gizmoType)
    {
        // 使用movement自己的OnDrawGizmos方法
        movement.DrawDebugLines();
    }
}
