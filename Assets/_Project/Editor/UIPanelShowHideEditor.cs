using UnityEditor;
using UnityEngine;
using _Project.UI.Animations;

/// <summary>
/// UIPanelShowHide 的条件 Inspector。
/// 通过功能开关控制参数显示，并按当前主体模式隐藏无关配置。
/// </summary>
[CustomEditor(typeof(UIPanelShowHide))]
public sealed class UIPanelShowHideEditor : Editor
{
    private SerializedProperty _panel;
    private SerializedProperty _group;
    private SerializedProperty _startHidden;
    private SerializedProperty _transitionRaycastMode;

    private SerializedProperty _transformAnimation;
    private SerializedProperty _mode;
    private SerializedProperty _duration;
    private SerializedProperty _showEase;
    private SerializedProperty _hideEase;
    private SerializedProperty _scaleOffset;
    private SerializedProperty _hiddenScale;

    private SerializedProperty _fadeAnimation;
    private SerializedProperty _backdropGroup;

    private SerializedProperty _animateChildren;
    private SerializedProperty _staggerRoot;
    private SerializedProperty _childStagger;
    private SerializedProperty _childDuration;
    private SerializedProperty _childHiddenScale;

    private void OnEnable()
    {
        _panel = serializedObject.FindProperty("_panel");
        _group = serializedObject.FindProperty("_group");
        _startHidden = serializedObject.FindProperty("_startHidden");
        _transitionRaycastMode = serializedObject.FindProperty("_transitionRaycastMode");

        _transformAnimation = serializedObject.FindProperty("_transformAnimation");
        _mode = serializedObject.FindProperty("_mode");
        _duration = serializedObject.FindProperty("_duration");
        _showEase = serializedObject.FindProperty("_showEase");
        _hideEase = serializedObject.FindProperty("_hideEase");
        _scaleOffset = serializedObject.FindProperty("_scaleOffset");
        _hiddenScale = serializedObject.FindProperty("_hiddenScale");

        _fadeAnimation = serializedObject.FindProperty("_fadeAnimation");
        _backdropGroup = serializedObject.FindProperty("_backdropGroup");

        _animateChildren = serializedObject.FindProperty("_animateChildren");
        _staggerRoot = serializedObject.FindProperty("_staggerRoot");
        _childStagger = serializedObject.FindProperty("_childStagger");
        _childDuration = serializedObject.FindProperty("_childDuration");
        _childHiddenScale = serializedObject.FindProperty("_childHiddenScale");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("公共配置", EditorStyles.boldLabel);
        DrawProperty(_panel, "面板主体");
        DrawProperty(_group, "交互 CanvasGroup");
        DrawProperty(_startHidden, "初始化时隐藏");
        DrawProperty(_transitionRaycastMode, "动画期间拦截底层输入");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("主体变换", EditorStyles.boldLabel);
        DrawProperty(_transformAnimation, "启用主体变换动画");
        if (_transformAnimation.boolValue)
        {
            EditorGUI.indentLevel++;
            DrawProperty(_mode, "隐藏模式");
            DrawProperty(_showEase, "显示缓动");
            DrawProperty(_hideEase, "隐藏缓动");
            if ((UIPanelShowHide.Mode)_mode.enumValueIndex == UIPanelShowHide.Mode.Scale)
            {
                DrawProperty(_scaleOffset, "缩放模式位移");
                DrawProperty(_hiddenScale, "隐藏缩放倍率");
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("透明度", EditorStyles.boldLabel);
        DrawProperty(_fadeAnimation, "启用透明度动画");
        if (_fadeAnimation.boolValue)
        {
            EditorGUI.indentLevel++;
            DrawProperty(_backdropGroup, "背景遮罩 CanvasGroup");
            EditorGUI.indentLevel--;
        }

        if (_transformAnimation.boolValue || _fadeAnimation.boolValue)
        {
            EditorGUI.indentLevel++;
            DrawProperty(_duration, "动画时长");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("直接子项错峰", EditorStyles.boldLabel);
        DrawProperty(_animateChildren, "启用子项错峰");
        if (_animateChildren.boolValue)
        {
            EditorGUI.indentLevel++;
            DrawProperty(_staggerRoot, "错峰根节点");
            DrawProperty(_childStagger, "子项间隔");
            DrawProperty(_childDuration, "子项时长");
            DrawProperty(_childHiddenScale, "子项隐藏缩放");
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawProperty(SerializedProperty property, string label)
    {
        if (property == null)
            return;

        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
    }
}
