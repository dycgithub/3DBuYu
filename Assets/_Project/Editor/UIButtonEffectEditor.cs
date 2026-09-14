using UnityEditor;
using UnityEngine;
using _Project.UI.Animations;

/// <summary>
/// UIButtonEffect 的条件 Inspector。
/// 先显示功能开关，再显示当前已启用功能的参数，避免无效配置占用 Inspector。
/// </summary>
[CustomEditor(typeof(UIButtonEffect))]
public sealed class UIButtonEffectEditor : Editor
{
    private SerializedProperty _visualRoot;
    private SerializedProperty _colorTarget;
    private SerializedProperty _ripple;
    private SerializedProperty _rippleGroup;

    private SerializedProperty _appearAnimation;
    private SerializedProperty _appearFrom;
    private SerializedProperty _appearDuration;
    private SerializedProperty _appearEase;

    private SerializedProperty _hoverAnimation;
    private SerializedProperty _hoverScale;
    private SerializedProperty _hoverLift;
    private SerializedProperty _hoverDuration;
    private SerializedProperty _hoverTilt;

    private SerializedProperty _pressAnimation;
    private SerializedProperty _pressedScaleX;
    private SerializedProperty _pressedScaleY;
    private SerializedProperty _pressedOffset;
    private SerializedProperty _pressedDuration;
    private SerializedProperty _releasedDuration;

    private SerializedProperty _rippleAnimation;
    private SerializedProperty _rippleDuration;
    private SerializedProperty _rippleScale;

    private SerializedProperty _soundEnabled;
    private SerializedProperty _hoverSfx;
    private SerializedProperty _pressSfx;
    private SerializedProperty _releaseSfx;
    private SerializedProperty _sfxVolume;
    private SerializedProperty _minimumPitch;
    private SerializedProperty _maximumPitch;

    private void OnEnable()
    {
        _visualRoot = serializedObject.FindProperty("_visualRoot");
        _colorTarget = serializedObject.FindProperty("_colorTarget");
        _ripple = serializedObject.FindProperty("_ripple");
        _rippleGroup = serializedObject.FindProperty("_rippleGroup");

        _appearAnimation = serializedObject.FindProperty("_appearAnimation");
        _appearFrom = serializedObject.FindProperty("_appearFrom");
        _appearDuration = serializedObject.FindProperty("_appearDuration");
        _appearEase = serializedObject.FindProperty("_appearEase");

        _hoverAnimation = serializedObject.FindProperty("_hoverAnimation");
        _hoverScale = serializedObject.FindProperty("_hoverScale");
        _hoverLift = serializedObject.FindProperty("_hoverLift");
        _hoverDuration = serializedObject.FindProperty("_hoverDuration");
        _hoverTilt = serializedObject.FindProperty("_hoverTilt");

        _pressAnimation = serializedObject.FindProperty("_pressAnimation");
        _pressedScaleX = serializedObject.FindProperty("_pressedScaleX");
        _pressedScaleY = serializedObject.FindProperty("_pressedScaleY");
        _pressedOffset = serializedObject.FindProperty("_pressedOffset");
        _pressedDuration = serializedObject.FindProperty("_pressedDuration");
        _releasedDuration = serializedObject.FindProperty("_releasedDuration");

        _rippleAnimation = serializedObject.FindProperty("_rippleAnimation");
        _rippleDuration = serializedObject.FindProperty("_rippleDuration");
        _rippleScale = serializedObject.FindProperty("_rippleScale");

        _soundEnabled = serializedObject.FindProperty("_soundEnabled");
        _hoverSfx = serializedObject.FindProperty("_hoverSfx");
        _pressSfx = serializedObject.FindProperty("_pressSfx");
        _releaseSfx = serializedObject.FindProperty("_releaseSfx");
        _sfxVolume = serializedObject.FindProperty("_sfxVolume");
        _minimumPitch = serializedObject.FindProperty("_minimumPitch");
        _maximumPitch = serializedObject.FindProperty("_maximumPitch");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        bool usesVisualRoot = _appearAnimation.boolValue ||
                              _hoverAnimation.boolValue ||
                              _pressAnimation.boolValue;
        if (usesVisualRoot)
        {
            EditorGUILayout.LabelField("引用", EditorStyles.boldLabel);
            DrawProperty(_visualRoot, "视觉节点");
        }

        if (_hoverAnimation.boolValue)
            DrawProperty(_colorTarget, "颜色目标");

        if (_rippleAnimation.boolValue)
        {
            if (!usesVisualRoot && !_hoverAnimation.boolValue)
                EditorGUILayout.LabelField("引用", EditorStyles.boldLabel);
            DrawProperty(_ripple, "光圈节点");
            DrawProperty(_rippleGroup, "光圈透明度组");
        }

        EditorGUILayout.Space();
        DrawAppearSection();
        DrawHoverSection();
        DrawPressSection();
        DrawRippleSection();
        DrawSoundSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawAppearSection()
    {
        EditorGUILayout.LabelField("出现动画", EditorStyles.boldLabel);
        DrawProperty(_appearAnimation, "启用出现动画");
        if (!_appearAnimation.boolValue)
            return;

        EditorGUI.indentLevel++;
        DrawProperty(_appearFrom, "初始缩放");
        DrawProperty(_appearDuration, "动画时长");
        DrawProperty(_appearEase, "缓动方式");
        EditorGUI.indentLevel--;
    }

    private void DrawHoverSection()
    {
        EditorGUILayout.LabelField("悬停动画", EditorStyles.boldLabel);
        DrawProperty(_hoverAnimation, "启用悬停动画");
        if (!_hoverAnimation.boolValue)
            return;

        EditorGUI.indentLevel++;
        DrawProperty(_hoverScale, "悬停缩放");
        DrawProperty(_hoverLift, "悬停上移");
        DrawProperty(_hoverDuration, "动画时长");
        DrawProperty(_hoverTilt, "随机倾斜角度");
        EditorGUI.indentLevel--;
    }

    private void DrawPressSection()
    {
        EditorGUILayout.LabelField("按下动画", EditorStyles.boldLabel);
        DrawProperty(_pressAnimation, "启用按下动画");
        if (!_pressAnimation.boolValue)
            return;

        EditorGUI.indentLevel++;
        DrawProperty(_pressedScaleX, "按下 X 缩放");
        DrawProperty(_pressedScaleY, "按下 Y 缩放");
        DrawProperty(_pressedOffset, "按下下移");
        DrawProperty(_pressedDuration, "按下时长");
        DrawProperty(_releasedDuration, "松开恢复时长");
        EditorGUI.indentLevel--;
    }

    private void DrawRippleSection()
    {
        EditorGUILayout.LabelField("点击光圈", EditorStyles.boldLabel);
        DrawProperty(_rippleAnimation, "启用点击光圈");
        if (!_rippleAnimation.boolValue)
            return;

        EditorGUI.indentLevel++;
        DrawProperty(_rippleDuration, "光圈时长");
        DrawProperty(_rippleScale, "结束缩放");
        EditorGUI.indentLevel--;
    }

    private void DrawSoundSection()
    {
        EditorGUILayout.LabelField("音效", EditorStyles.boldLabel);
        DrawProperty(_soundEnabled, "启用音效");
        if (!_soundEnabled.boolValue)
            return;

        EditorGUI.indentLevel++;
        DrawProperty(_hoverSfx, "悬停音效名称");
        DrawProperty(_pressSfx, "按下音效名称");
        DrawProperty(_releaseSfx, "松开音效名称");
        DrawProperty(_sfxVolume, "音量");
        DrawProperty(_minimumPitch, "最小音调倍率");
        DrawProperty(_maximumPitch, "最大音调倍率");
        EditorGUI.indentLevel--;
    }

    private static void DrawProperty(SerializedProperty property, string label)
    {
        if (property == null)
            return;

        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
    }
}
