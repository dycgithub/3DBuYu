using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TurretSystem;

/// <summary>
/// Turret 自定义 Editor。
/// - Scene View 中显示球冠轮廓和八个端口位置
/// - 点击端口球体可选中对应的 TurretPortConfig 资产
/// - 距离标注 + 球冠底面圆周可视化
/// - 运行时调试面板（属性只读面板 + 强制开火按钮）
/// </summary>
[CustomEditor(typeof(Turret))]
public class TurretEditor : Editor
{
    #region 端口布局调试字段

    private bool _showPortLayout = true;
    private bool _showDistanceLabels = true;
    private bool _showLayoutRing = true;
    private float _ringHeightOffset = 0f;
    private float _labelHeightOffset = 0.15f;
    private float _capHeightEdit = 1f;

    private static readonly Color ActivePortColor = new Color(1f, 0.8f, 0.2f, 0.6f);
    private static readonly Color LockedPortColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
    private static readonly Color RingColor = new Color(0f, 0.7f, 1f, 0.3f);

    #endregion

    #region Inspector

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var turret = (Turret)target;

        // ── 端口布局调试（编辑模式 + 运行模式均可用） ──
        DrawPortLayoutSection(turret);

        EditorGUILayout.Space();

        // ── 运行时调试 ──
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("进入 Play 模式以使用运行时调试功能。", MessageType.Info);
            return;
        }

        DrawRuntimeDebugSection(turret);
    }

    /// <summary>
    /// 端口布局调试区域。
    /// </summary>
    private void DrawPortLayoutSection(Turret turret)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("── 端口布局调试 ──", EditorStyles.boldLabel);

        _showPortLayout = EditorGUILayout.Toggle("显示端口布局 (Scene View)", _showPortLayout);
        if (_showPortLayout)
        {
            EditorGUI.indentLevel++;
            _showDistanceLabels = EditorGUILayout.Toggle("距离标签", _showDistanceLabels);
            _showLayoutRing = EditorGUILayout.Toggle("球冠底面圆周", _showLayoutRing);
            _ringHeightOffset = EditorGUILayout.Slider("环高度偏移", _ringHeightOffset, -1f, 1f);
            _labelHeightOffset = EditorGUILayout.Slider("标签高度", _labelHeightOffset, 0f, 1f);
            EditorGUI.indentLevel--;
        }

        // 球冠高度预览
        if (turret.TurretBaseConfig != null)
        {
            EditorGUILayout.Space();
            float newCapHeight = EditorGUILayout.Slider("预览球冠高度", _capHeightEdit, 0f, 10f);
            if (newCapHeight != _capHeightEdit)
            {
                _capHeightEdit = newCapHeight;
                SceneView.RepaintAll();
            }
            EditorGUILayout.HelpBox(
                "运行时使用 TurretBase 资产中的 capHeight 值。\n" +
                "场景中的预览高度仅在编辑器中生效。",
                MessageType.Info);
        }
    }

    /// <summary>
    /// 运行时调试区域。
    /// </summary>
    private void DrawRuntimeDebugSection(Turret turret)
    {
        EditorGUILayout.LabelField("── 运行时调试 ──", EditorStyles.boldLabel);

        // 属性只读面板
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.FloatField("当前射程", turret.Range);
        EditorGUILayout.FloatField("当前伤害", turret.Damage);
        EditorGUILayout.FloatField("当前射速", turret.FireRate);
        EditorGUILayout.IntField("端口数量", turret.PortCount);
        EditorGUILayout.IntField("活跃端口", turret.ActivePortCount);
        EditorGUILayout.IntField("锁定端口", turret.LockedPortCount);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();

        // 按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("解锁下一端口"))
        {
            var unlocked = turret.TryExpandPort();
            if (unlocked != null)
                Debug.Log($"[TurretEditor] {turret.name}: 已解锁 {unlocked.PortId}");
            else
                Debug.Log($"[TurretEditor] {turret.name}: 没有可解锁的端口。");
        }
        EditorGUILayout.EndHorizontal();

        // ── 端口库存预览 ──
        if (turret.PortManager != null)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("── 端口库存预览 ──", EditorStyles.boldLabel);

            if (turret.TurretInventory != null)
            {
                var tGrid = turret.TurretInventory.Grid;
                EditorGUILayout.LabelField($"炮台库存 [{turret.TurretInventory.Grid.GetAllItems().Count} 件]",
                    EditorStyles.miniBoldLabel);
            }

            int portIdx = 0;
            foreach (var port in turret.PortManager.Ports)
            {
                portIdx++;
                var pStats = port.Inventory?.Attributes;
                string info = port.IsLocked ? "【锁定】" : "【活跃】";
                if (pStats != null && !port.IsLocked)
                    info += $" Dmg:{pStats.Damage:F0} Rng:{pStats.Range:F1} FR:{pStats.FireRate:F2}";
                EditorGUILayout.LabelField($"  P{portIdx} {info}", EditorStyles.miniLabel);
            }
        }
    }

    #endregion

    #region Scene View 端口手柄

    private void OnSceneGUI()
    {
        var turret = (Turret)target;
        if (turret.TurretBaseConfig == null) return;
        if (!_showPortLayout) return;

        var configs = turret.TurretBaseConfig.firingPorts;
        if (configs == null || configs.Length == 0) return;

        Transform turretTransform = turret.transform;
        Vector3 turretPos = turretTransform.position;
        bool isPlayMode = Application.isPlaying;

        // 获取球心和球冠高度
        SphereWalker sphereWalker = turret.SphereWalker;
        bool hasSphereCenter = sphereWalker != null;
        Vector3 sphereCenter = hasSphereCenter ? sphereWalker.GetEffectiveCenter() : Vector3.zero;
        float capHeight = hasSphereCenter ? _capHeightEdit : turret.TurretBaseConfig.capHeight;

        // ── 绘制球冠底面圆周 ──
        if (_showLayoutRing && hasSphereCenter)
        {
            Vector3[] basePoints = TurretCapLayout.GetCapBaseCircle(sphereCenter, turretPos, capHeight, 48);
            Handles.color = RingColor;
            for (int i = 0; i < basePoints.Length; i++)
            {
                int next = (i + 1) % basePoints.Length;
                if (i % 2 == 0)
                    Handles.DrawLine(basePoints[i], basePoints[next]);
            }

            // 底面圆心标记
            Vector3 normal = (turretPos - sphereCenter).normalized;
            float R = Vector3.Distance(turretPos, sphereCenter);
            float h = Mathf.Clamp(capHeight, 0f, R);
            Vector3 capBaseCenter = sphereCenter + normal * (R - h);
            float centerSize = HandleUtility.GetHandleSize(capBaseCenter) * 0.04f;
            Handles.SphereHandleCap(0, capBaseCenter, Quaternion.identity, centerSize, EventType.Repaint);

            // 球冠轮廓弧线（从顶点到底面圆周的 4 条经线）
            Handles.color = new Color(RingColor.r, RingColor.g, RingColor.b, 0.15f);
            SphericalCoordinates.GetTangentBasis(normal, out Vector3 east, out Vector3 north);
            float a = Mathf.Sqrt(2f * R * h - h * h);
            for (int m = 0; m < 4; m++)
            {
                float arcAngle = (m / 4f) * Mathf.PI * 2f;
                Vector3 baseDir = (east * Mathf.Cos(arcAngle) + north * Mathf.Sin(arcAngle)).normalized;
                Vector3 basePoint = capBaseCenter + baseDir * a;
                Handles.DrawBezier(turretPos, basePoint,
                    turretPos - normal * (R - h) * 0.5f,
                    basePoint + normal * (R - h) * 0.5f,
                    RingColor, null, 2f);
            }
        }

        // ── 绘制每个端口 ──
        for (int i = 0; i < configs.Length; i++)
        {
            var config = configs[i];
            if (config == null) continue;

            // 计算端口世界位置
            Vector3 worldPos;

            if (isPlayMode)
            {
                var portManager = turret.PortManager;
                if (portManager != null)
                {
                    var port = portManager.GetPort(i);
                    if (port?.FirePoint == null) continue;
                    worldPos = port.FirePoint.position;
                }
                else
                {
                    continue;
                }
            }
            else
            {
                if (hasSphereCenter)
                {
                    TurretCapLayout.CalculatePortPose(
                        sphereCenter, turretPos, capHeight,
                        config.portName,
                        out worldPos);
                }
                else
                {
                    continue;
                }
            }

            bool isLocked = isPlayMode
                ? (turret.PortManager?.GetPort(i)?.IsLocked ?? config.isInitiallyLocked)
                : config.isInitiallyLocked;

            float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.12f;
            Handles.color = isLocked ? LockedPortColor : ActivePortColor;

            // ── 可点击的端口球体标记 ──
            if (Handles.Button(worldPos, turretTransform.rotation, handleSize, handleSize * 1.2f, Handles.SphereHandleCap))
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }

            // ── 距离标签（显示到 turret 的球面弧距） ──
            if (_showDistanceLabels)
            {
                float arcDistance = Vector3.Distance(turretPos, worldPos);
                var labelStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = isLocked ? Color.gray : Color.white },
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter
                };
                Vector3 labelPos = worldPos + (turretPos - sphereCenter).normalized * _labelHeightOffset;
                Handles.Label(labelPos, $"{config.portName}\n{arcDistance:F2}m", labelStyle);
            }

            // ── turret 到端口的虚线 ──
            Handles.color = isLocked ? LockedPortColor : ActivePortColor;
            Handles.DrawDottedLine(turretPos, worldPos, 4f);

            // ── 端口十字标记 ──
            float crossSize = handleSize * 0.5f;
            Handles.DrawLine(worldPos + turretTransform.right * crossSize, worldPos - turretTransform.right * crossSize);
            Handles.DrawLine(worldPos + turretTransform.up * crossSize, worldPos - turretTransform.up * crossSize);
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 绘制虚线圆环。
    /// </summary>
    private static void DrawDashedCircle(Vector3 center, Vector3 normal, float radius, int segments)
    {
        if (segments < 4) segments = 4;

        Quaternion rotation = Quaternion.LookRotation(normal);
        Vector3 prevPoint = center + rotation * new Vector3(Mathf.Sin(0), 0, Mathf.Cos(0)) * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = (360f / segments) * i * Mathf.Deg2Rad;
            Vector3 point = center + rotation * new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;

            if (i % 2 == 0)
                Handles.DrawLine(prevPoint, point);

            prevPoint = point;
        }
    }

    #endregion
}
