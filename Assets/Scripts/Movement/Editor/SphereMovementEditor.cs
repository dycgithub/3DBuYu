using UnityEngine;
using UnityEditor;

namespace SphereMovement
{
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
                "targetObject");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10f);

            // 调试信息
            _showPreview = EditorGUILayout.Foldout(_showPreview, "调试信息");
            if (_showPreview)
            {
                EditorGUI.indentLevel++;

                Transform targetObj = _movement.TargetObject;
                EditorGUILayout.ObjectField("当前目标物体", targetObj, typeof(Transform), true);

                Vector3 pos = _movement.CurrentPositionOnSphere;
                EditorGUILayout.Vector3Field("球面位置", pos);

                float lonDegrees = _movement.CurrentLongitude * Mathf.Rad2Deg;
                float latDegrees = _movement.CurrentLatitude * Mathf.Rad2Deg;
                EditorGUILayout.LabelField($"经度: {lonDegrees:F1}°");
                EditorGUILayout.LabelField($"纬度: {latDegrees:F1}°");

                EditorGUI.indentLevel--;
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        private static void DrawGizmoForSphereMovement(SphereMovement movement, GizmoType gizmoType)
        {
            // Gizmos由SphereMovementGizmos组件绘制
        }
    }
}
