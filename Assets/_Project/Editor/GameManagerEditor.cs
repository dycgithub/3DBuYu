using UnityEditor;
using UnityEngine;
using GameSystem;
using ItemSystem.Functions;

/// <summary>
/// GameManager 自定义 Editor — 状态机控制 + 主动技能调试按钮。
/// </summary>
[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var gm = (GameManager)target;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("进入 Play 模式以使用调试功能。", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("── 状态机调试 ──", EditorStyles.boldLabel);

        GameState state = gm.CurrentState;
        var stateRect = EditorGUILayout.GetControlRect(false, 24f);
        EditorGUI.DrawRect(stateRect, state switch
        {
            GameState.Playing => new Color(0.2f, 0.8f, 0.2f),
            GameState.Settled => new Color(0.2f, 0.8f, 0.8f),
            GameState.Failed => new Color(0.8f, 0.2f, 0.2f),
            _ => Color.gray
        });
        EditorGUI.LabelField(stateRect, $"  当前状态: {state}", EditorStyles.whiteBoldLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"本局积分: {gm.SessionPoints}");

        EditorGUILayout.Space();

        // 控制按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("开始关卡"))
        {
            gm.StartLevel();
            Debug.Log("[GameManagerEditor] 开始关卡。");
        }
        if (GUILayout.Button("结算(胜利)"))
        {
            gm.Settle();
            Debug.Log("[GameManagerEditor] 结算。");
        }
        if (GUILayout.Button("游戏结束(失败)"))
        {
            gm.GameOver();
            Debug.Log("[GameManagerEditor] 游戏结束。");
        }
        EditorGUILayout.EndHorizontal();

        // 主动技能调试(命令模式触发接口的编辑器入口)
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("── 主动技能调试 ──", EditorStyles.boldLabel);

        var skillManager = gm.SkillManager;
        if (skillManager == null || skillManager.Count == 0)
        {
            EditorGUILayout.HelpBox("未检测到已装备技能(炮塔格需放置 Skill 类物品并开始关卡)。", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"已装技能: {skillManager.Count} 个");
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < skillManager.Count; i++)
        {
            string label = $"{skillManager.GetName(i)}({(skillManager.IsReady(i) ? "就绪" : $"{skillManager.GetCooldownRemaining(i):F1}s")})";
            int slot = i;
            if (GUILayout.Button($"释放 {label}"))
            {
                gm.TryExecuteSkill(slot);
            }
        }
        EditorGUILayout.EndHorizontal();
    }
}
