using UnityEditor;
using UnityEngine;

/// <summary>只在 Inspector 提供轨道测试按钮，不创建游戏运行时 UI。</summary>
[CustomEditor(typeof(SphereWalker))]
public sealed class SphereWalkerEditor : Editor
{
    private float _step = 1f;

    /// <summary>运行时按钮调用与自动调节共用的轨道 API。</summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        _step = Mathf.Max(0.1f, EditorGUILayout.FloatField("测试增减量", _step));
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            SphereWalker walker = (SphereWalker)target;
            if (GUILayout.Button("扩大轨道"))
            {
                walker.AdjustRadius(_step);
            }
            if (GUILayout.Button("缩小轨道"))
            {
                walker.AdjustRadius(-_step);
            }
            if (GUILayout.Button("恢复数量曲线控制"))
            {
                walker.ResetRadius();
            }
        }
    }
}
