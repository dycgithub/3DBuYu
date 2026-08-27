#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 道具生成测试工具(EditorWindow,目录驱动):
/// - 目标网格:下拉列出场景所有 GridView(编辑/Play 模式均可用)
/// - 物品目录:ObjectField 选择 ItemCatalog 资产,打开窗口自动加载 SO/Item/ItemCatalog.asset
/// - 目录内物品:下拉列出所选目录的 ItemDefinition
/// - 生成:扫描目标网格第一个空位放置所选定义
/// 纯 C# 构建 UI,自包含,不依赖 UXML 资产绑定。
/// </summary>
public class InventoryTestWindow : EditorWindow
{
    private const string SampleAssetDir = "Assets/_Project/SO/Item";
    private const string DefaultCatalogPath = "Assets/_Project/SO/Item/ItemCatalog.asset";

    private readonly List<GridView> _grids = new();
    private readonly List<ItemDefinition> _items = new();

    private DropdownField _gridDropdown;
    private ObjectField _catalogField;
    private DropdownField _itemDropdown;
    private Label _statusLabel;

    [MenuItem("Window/Inventory/道具生成测试")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<InventoryTestWindow>();
        wnd.titleContent = new GUIContent("Inventory 道具生成测试");
        wnd.minSize = new Vector2(380, 260);
    }

    public void CreateGUI()
    {
        var root = rootVisualElement;

        root.Add(new Label("道具生成测试工具") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 } });

        _gridDropdown = new DropdownField("目标网格");
        _gridDropdown.RegisterValueChangedCallback(_ => RefreshStatus());
        root.Add(_gridDropdown);

        _catalogField = new ObjectField("物品目录") { objectType = typeof(ItemCatalog), allowSceneObjects = false };
        _catalogField.RegisterValueChangedCallback(evt => OnCatalogChanged(evt.newValue as ItemCatalog));
        root.Add(_catalogField);

        _itemDropdown = new DropdownField("目录内物品");
        _itemDropdown.RegisterValueChangedCallback(_ => RefreshStatus());
        root.Add(_itemDropdown);

        var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
        var refreshButton = new Button(RefreshCatalog) { text = "刷新目录" };
        var registerButton = new Button(RegisterDefinitionsToCatalog) { text = "注册目录内定义" };
        var sampleButton = new Button(CreateSampleAssets) { text = "创建示例资产" };
        buttonRow.Add(refreshButton);
        buttonRow.Add(registerButton);
        buttonRow.Add(sampleButton);
        root.Add(buttonRow);

        var spawnButton = new Button(SpawnItem) { text = "生成道具", style = { marginTop = 10 } };
        root.Add(spawnButton);

        _statusLabel = new Label("就绪") { style = { marginTop = 8, whiteSpace = WhiteSpace.Normal } };
        root.Add(_statusLabel);

        RefreshGrids();
        LoadDefaultCatalog();
    }

    private void OnFocus()
    {
        RefreshGrids();
        LoadDefaultCatalog();
    }

    /// <summary>刷新场景网格列表。</summary>
    private void RefreshGrids()
    {
        _grids.Clear();
        _grids.AddRange(FindObjectsByType<GridView>(FindObjectsSortMode.None));

        _gridDropdown.choices.Clear();
        foreach (var g in _grids)
            _gridDropdown.choices.Add($"{g.name} ({g.GridType})");
        if (_grids.Count > 0 && string.IsNullOrEmpty(_gridDropdown.value))
            _gridDropdown.value = _gridDropdown.choices[0];

        RefreshStatus();
    }

    /// <summary>打开窗口/聚焦时:未手动选择目录则自动加载默认目录资产。</summary>
    private void LoadDefaultCatalog()
    {
        if (_catalogField.value != null) return; // 已有选择,不覆盖

        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(DefaultCatalogPath);
        if (catalog != null)
        {
            _catalogField.value = catalog; // 触发 OnCatalogChanged 填充物品列表
        }
        else
        {
            _items.Clear();
            RefreshItemDropdown();
        }
    }

    /// <summary>重新加载当前目录(资产可能被外部修改)。</summary>
    private void RefreshCatalog()
    {
        var current = _catalogField.value as ItemCatalog;
        if (current == null)
        {
            LoadDefaultCatalog();
            return;
        }

        var path = AssetDatabase.GetAssetPath(current);
        var reloaded = AssetDatabase.LoadAssetAtPath<ItemCatalog>(path);
        _catalogField.SetValueWithoutNotify(reloaded);
        OnCatalogChanged(reloaded);
    }

    /// <summary>目录变化 → 填充"目录内物品"下拉。</summary>
    private void OnCatalogChanged(ItemCatalog catalog)
    {
        _items.Clear();
        if (catalog != null && catalog.Items != null)
            _items.AddRange(catalog.Items);
        RefreshItemDropdown();
    }

    private void RefreshItemDropdown()
    {
        _itemDropdown.choices.Clear();
        foreach (var item in _items)
            _itemDropdown.choices.Add($"{item.DisplayName} ({item.Id}, {item.Shape})");
        if (_items.Count > 0 && string.IsNullOrEmpty(_itemDropdown.value))
            _itemDropdown.value = _itemDropdown.choices[0];

        RefreshStatus();
    }

    /// <summary>
    /// 扫描 SampleAssetDir 下所有 ItemDefinition 资产并注册进当前目录(去重,不覆盖)。
    /// 用于把已手动创建的资产(如 ItemDefinition 1-8.asset)纳入目录。
    /// </summary>
    private void RegisterDefinitionsToCatalog()
    {
        var catalog = _catalogField.value as ItemCatalog;
        if (catalog == null)
        {
            catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(DefaultCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, DefaultCatalogPath);
            }
        }

        var guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { SampleAssetDir });
        int added = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null) continue;

            int before = catalog.Items.Count;
            catalog.Add(def);
            if (catalog.Items.Count > before) added++;
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _catalogField.value = catalog; // 触发 OnCatalogChanged,物品列表刷新
        _statusLabel.text = $"已注册 {added} 个定义到目录(目录共 {_items.Count} 个)";
    }

    /// <summary>生成 9 个示例定义(覆盖全部形状/不同颜色),并同步到 ItemCatalog 资产。</summary>
    private void CreateSampleAssets()
    {
        EnsureSampleDir();

        // id 必须唯一:同 id 会因“已存在则跳过”导致后续形状永远不会创建
        var samples = new (string id, string name, ItemShape shape, Color color)[]
        {
            ("sample_single", "单格", ItemShape.Single, Color.white),
            ("sample_v2", "竖二", ItemShape.Vertical2, new Color(1f, 0.4f, 0.4f)),
            ("sample_h2", "横二", ItemShape.Horizontal2, new Color(0.4f, 1f, 0.4f)),
            ("sample_square", "方块", ItemShape.Square2x2, new Color(0.4f, 0.6f, 1f)),
            ("sample_l1", "L形1", ItemShape.LShape1, new Color(1f, 0.9f, 0.4f)),
            ("sample_l2", "L形2", ItemShape.LShape2, new Color(1f, 0.8f, 0.3f)),
            ("sample_l3", "L形3", ItemShape.LShape3, new Color(1f, 0.7f, 0.2f)),
            ("sample_t1", "T形1", ItemShape.TShape1, new Color(0.8f, 0.4f, 1f)),
            ("sample_t2", "T形2", ItemShape.TShape2, new Color(0.7f, 0.3f, 0.9f)),
        };

        foreach (var (id, name, shape, color) in samples)
        {
            var path = $"{SampleAssetDir}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) != null) continue; // 已存在则跳过

            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(def, path);

            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("shape").enumValueIndex = (int)shape;
            so.FindProperty("color").colorValue = color;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 生成/更新目录资产,示例定义全部注册进去(商店/存档按 id 查询可用)
        var catalogPath = $"{SampleAssetDir}/ItemCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }
        foreach (var (id, _, _, _) in samples)
        {
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{SampleAssetDir}/{id}.asset");
            if (def != null) catalog.Add(def);
        }
        EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _catalogField.value = catalog; // 触发 OnCatalogChanged,物品列表指向新目录
        _statusLabel.text = $"已创建/确认示例资产与目录({_items.Count} 个定义)";
    }

    private static void EnsureSampleDir()
    {
        if (AssetDatabase.IsValidFolder(SampleAssetDir)) return;
        if (!AssetDatabase.IsValidFolder("Assets/_Project")) return;

        if (!AssetDatabase.IsValidFolder("Assets/_Project/SO"))
            AssetDatabase.CreateFolder("Assets/_Project", "SO");
        if (!AssetDatabase.IsValidFolder(SampleAssetDir))
            AssetDatabase.CreateFolder("Assets/_Project/SO", "Item");
    }

    private GridView SelectedGrid
    {
        get
        {
            int index = _gridDropdown.index;
            return index >= 0 && index < _grids.Count ? _grids[index] : null;
        }
    }

    private ItemDefinition SelectedItem
    {
        get
        {
            int index = _itemDropdown.index;
            return index >= 0 && index < _items.Count ? _items[index] : null;
        }
    }

    private void SpawnItem()
    {
        var grid = SelectedGrid;
        if (grid == null)
        {
            _statusLabel.text = "未找到目标网格(场景中需存在 GridView)";
            return;
        }

        var def = SelectedItem;
        if (def == null)
        {
            _statusLabel.text = "目录为空或未选择物品(可先点“创建示例资产”)";
            return;
        }

        grid.EnsureGridVM();

        // 扫描第一个空位放置(失败返回 null 则继续下一格)
        for (int r = 0; r < grid.GridVM.Height; r++)
        {
            for (int c = 0; c < grid.GridVM.Width; c++)
            {
                var view = grid.SpawnItem(def, c, r);
                if (view != null)
                {
                    Undo.RegisterCreatedObjectUndo(view.gameObject, "Spawn Item");
                    _statusLabel.text = $"已生成 {def.DisplayName} ({def.Shape}) → ({c},{r}) @ {grid.name}";
                    EditorUtility.SetDirty(grid.gameObject);
                    return;
                }
            }
        }

        _statusLabel.text = "网格已满,无法放置";
    }

    private void RefreshStatus()
    {
        if (_statusLabel == null) return;

        var grid = SelectedGrid;
        var def = SelectedItem;
        if (grid == null)
            _statusLabel.text = "未选择网格";
        else if (def == null)
            _statusLabel.text = $"目标: {grid.name} ({grid.GridType}) — 目录为空或未选择物品";
        else
            _statusLabel.text = $"目标: {grid.name} ({grid.GridType}) | 物品: {def.DisplayName} ({def.Id}, {def.Shape})";
    }
}
#endif
