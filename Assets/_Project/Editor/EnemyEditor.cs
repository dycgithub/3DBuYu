using UnityEditor;
using UnityEngine;
using EnemySystem;
using FlockingSystem.ECS;
using CombatSystem;

/// <summary>
/// Enemy 自定义 Editor，显示运行时血条、Flocking 状态和伤害测试按钮。
/// </summary>
[CustomEditor(typeof(Enemy), true)]
public class EnemyEditor : Editor
{
    /// <summary>
    /// 绘制敌人默认 Inspector 和运行时调试控件。
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Enemy enemyComponent = (Enemy)target;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("进入 Play 模式以使用调试功能。", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("── 运行时调试 ──", EditorStyles.boldLabel);

        // 血条
        float healthPercent = enemyComponent.HealthPercent;
        Color healthColor = healthPercent > 0.5f
            ? Color.green
            : (healthPercent > 0.25f ? Color.yellow : Color.red);
        Rect healthBarRect = EditorGUILayout.GetControlRect(false, 20f);
        EditorGUI.DrawRect(
            new Rect(
                healthBarRect.x,
                healthBarRect.y,
                healthBarRect.width * healthPercent,
                healthBarRect.height),
            healthColor);
        EditorGUI.LabelField(
            healthBarRect,
            $"  HP: {enemyComponent.CurrentHealth:F0} / {enemyComponent.MaxHealth:F0} " +
            $"({healthPercent * 100f:F0}%)");

        EditorGUILayout.Space();

        // 存活状态
        bool enemyIsDead = enemyComponent.IsDead;
        Rect stateRect = EditorGUILayout.GetControlRect(false, 20f);
        EditorGUI.DrawRect(stateRect, enemyIsDead ? Color.black : new Color(0.3f, 0.5f, 1f));
        GUIStyle stateLabelStyle = new GUIStyle(EditorStyles.label);
        stateLabelStyle.normal.textColor = enemyIsDead ? Color.white : Color.black;
        EditorGUI.LabelField(
            stateRect,
            $"  状态: {(enemyIsDead ? "已死亡" : "存活")}",
            stateLabelStyle);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"类型: {enemyComponent.EnemyType}");
        EditorGUILayout.LabelField($"是否死亡: {enemyIsDead}");

        // ECS Flocking 状态
        EnemyFlockBridge flockBridge = enemyComponent.GetComponent<EnemyFlockBridge>();
        if (flockBridge != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("── ECS Flocking ──", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ECS 接管: {flockBridge.IsEcsControlled}");
            EditorGUILayout.LabelField($"Slot: {flockBridge.Slot}");
        }

        EditorGUILayout.Space();

        // 伤害测试按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("造成 10 伤害"))
        {
            ApplyDamage(enemyComponent, 10f);
            Debug.Log($"[EnemyEditor] {enemyComponent.name}: 造成 10 伤害, 剩余 HP: {enemyComponent.CurrentHealth}");
        }
        if (GUILayout.Button("造成 50 伤害"))
        {
            ApplyDamage(enemyComponent, 50f);
            Debug.Log($"[EnemyEditor] {enemyComponent.name}: 造成 50 伤害, 剩余 HP: {enemyComponent.CurrentHealth}");
        }
        if (GUILayout.Button("击杀"))
        {
            ApplyDamage(enemyComponent, enemyComponent.CurrentHealth);
            Debug.Log($"[EnemyEditor] {enemyComponent.name}: 强制击杀。");
        }
        EditorGUILayout.EndHorizontal();
    }

    private static void ApplyDamage(Enemy enemy, float amount)
    {
        DamageRequest request = new DamageRequest
        {
            DamageAmount = amount,
            DamageType = DamageType.Physical,
            HitPosition = enemy.transform.position,
            HitNormal = enemy.transform.position.normalized
        };
        enemy.ReceiveDamageRequest(request);
    }
}
