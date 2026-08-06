using UnityEditor;
using UnityEngine;
using EnemySystem;
using FlockingSystem;

/// <summary>
/// Enemy 自定义 Editor — 运行时血条、Flocking 状态、伤害测试按钮。
/// </summary>
[CustomEditor(typeof(Enemy), true)]
public class EnemyBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var enemy = (Enemy)target;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("进入 Play 模式以使用调试功能。", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("── 运行时调试 ──", EditorStyles.boldLabel);

        // 血条
        float hpPercent = enemy.HealthPercent;
        Color hpColor = hpPercent > 0.5f ? Color.green : (hpPercent > 0.25f ? Color.yellow : Color.red);
        var hpRect = EditorGUILayout.GetControlRect(false, 20f);
        EditorGUI.DrawRect(new Rect(hpRect.x, hpRect.y, hpRect.width * hpPercent, hpRect.height), hpColor);
        EditorGUI.LabelField(hpRect, $"  HP: {enemy.CurrentHealth:F0} / {enemy.MaxHealth:F0} ({hpPercent * 100f:F0}%)");

        EditorGUILayout.Space();

        // 存活状态
        bool isDead = enemy.IsDead;
        var stateRect = EditorGUILayout.GetControlRect(false, 20f);
        EditorGUI.DrawRect(stateRect, isDead ? Color.black : new Color(0.3f, 0.5f, 1f));
        var style = new GUIStyle(EditorStyles.label);
        style.normal.textColor = isDead ? Color.white : Color.black;
        EditorGUI.LabelField(stateRect, $"  状态: {(isDead ? "已死亡" : "存活")}", style);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"类型: {enemy.EnemyType}");
        EditorGUILayout.LabelField($"是否死亡: {isDead}");

        // FlockAgent 信息
        var flockAgent = enemy.GetComponent<FlockAgent>();
        if (flockAgent != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("── Flocking ──", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"当前速度: {flockAgent.Speed:F1}");
            EditorGUILayout.LabelField($"速度倍率: {flockAgent.SpeedMultiplier:F2}");
            EditorGUILayout.LabelField($"邻居距离: {flockAgent.NeighbourDistance:F1}");
            EditorGUILayout.LabelField($"分离距离: {flockAgent.SeparationDistance:F1}");

            var manager = flockAgent.Manager;
            if (manager != null)
            {
                EditorGUILayout.LabelField($"群组大小: {manager.Agents.Count}");
                EditorGUILayout.LabelField($"群游目标: {manager.GoalPos}");
            }
        }

        EditorGUILayout.Space();

        // 伤害测试按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("造成 10 伤害"))
        {
            enemy.TakeDamage(10f);
            Debug.Log($"[EnemyBaseEditor] {enemy.name}: 造成 10 伤害, 剩余 HP: {enemy.CurrentHealth}");
        }
        if (GUILayout.Button("造成 50 伤害"))
        {
            enemy.TakeDamage(50f);
            Debug.Log($"[EnemyBaseEditor] {enemy.name}: 造成 50 伤害, 剩余 HP: {enemy.CurrentHealth}");
        }
        if (GUILayout.Button("击杀"))
        {
            enemy.TakeDamage(enemy.CurrentHealth);
            Debug.Log($"[EnemyBaseEditor] {enemy.name}: 强制击杀。");
        }
        EditorGUILayout.EndHorizontal();
    }
}
