using UnityEngine;
using CombatSystem;

/// <summary>
/// 物品定义(对齐 Cholopol TIS 的 ItemDetails):
/// 一个资产 = 一种物品类型;多个 ItemDefinition 即"多种 item"。
/// 技能数组保持配置顺序，战斗运行时按顺序组合为一张卡。
/// </summary>
[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Inventory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("身份")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField][TextArea] private string description;

    [Header("表现")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Sprite boundingBoxImage;
    [SerializeField] private Color color = Color.white;

    [Header("网格形状")]
    [SerializeField] private ItemShape shape = ItemShape.Single;

    [Header("商店价格")]
    [SerializeField] private int price;

    [Header("战斗配置")]
    [SerializeField] private CombatSystem.ItemCombatDefinitionSO combatDefinition;
    [SerializeField] private CombatSystem.SkillDefinitionSO[] skillDefinitions =
        System.Array.Empty<CombatSystem.SkillDefinitionSO>();

    /// <summary>获取物品定义的稳定标识。</summary>
    public string Id => id;

    /// <summary>获取物品在界面中显示的名称。</summary>
    public string DisplayName => displayName;

    /// <summary>获取物品的描述文本。</summary>
    public string Description => description;

    /// <summary>获取物品图标。</summary>
    public Sprite Icon => icon;

    /// <summary>获取覆盖物品包围盒并由占用形状裁切的图片。</summary>
    public Sprite BoundingBoxImage => boundingBoxImage;

    /// <summary>获取物品在界面中的显示颜色。</summary>
    public Color Color => color;

    /// <summary>获取物品占用的网格形状。</summary>
    public ItemShape Shape => shape;

    /// <summary>商店售价(积分,Points)。0 = 免费商品。</summary>
    public int Price => price;
    /// <summary>获取该物品的显式战斗配置；未配置时不产生战斗配置效果。</summary>
    public CombatSystem.ItemCombatDefinitionSO CombatDefinition => combatDefinition;

    /// <summary>获取按配置顺序排列的技能定义。</summary>
    public CombatSystem.SkillDefinitionSO[] SkillDefinitions => skillDefinitions;

    /// <summary>
    /// 运行时创建临时定义:测试窗口 / 商店程序化生成 / 无资产环境使用。
    /// 正式配置请用 CreateAssetMenu 创建资产。
    /// </summary>
    /// <param name="itemId">物品定义标识。</param>
    /// <param name="itemName">物品显示名称。</param>
    /// <param name="itemIcon">物品图标，可为空。</param>
    /// <param name="itemShape">物品占用的网格形状。</param>
    /// <param name="itemColor">物品显示颜色；默认值会回退为白色。</param>
    /// <returns>未保存到工程的运行时物品定义。</returns>
    public static ItemDefinition CreateRuntime(string itemId, string itemName, Sprite itemIcon, ItemShape itemShape, Color itemColor = default)
    {
        var def = CreateInstance<ItemDefinition>();
        def.id = itemId;
        def.displayName = itemName;
        def.description = string.Empty;
        def.icon = itemIcon;
        def.shape = itemShape;
        def.color = itemColor.a <= 0f ? Color.white : itemColor;
        return def;
    }

    /// <summary>创建带战斗配置的运行时物品定义，保留原便捷构造函数。</summary>
    public static ItemDefinition CreateRuntime(
        string itemId,
        string itemName,
        Sprite itemIcon,
        ItemShape itemShape,
        CombatSystem.ItemCombatDefinitionSO combatDefinition)
    {
        ItemDefinition definition = CreateRuntime(itemId, itemName, itemIcon, itemShape);
        definition.combatDefinition = combatDefinition;
        return definition;
    }

    /// <summary>创建带战斗配置和有序技能数组的运行时物品定义。</summary>
    public static ItemDefinition CreateRuntime(
        string itemId,
        string itemName,
        Sprite itemIcon,
        ItemShape itemShape,
        CombatSystem.ItemCombatDefinitionSO combatDefinition,
        params CombatSystem.SkillDefinitionSO[] itemSkillDefinitions)
    {
        ItemDefinition definition = CreateRuntime(
            itemId,
            itemName,
            itemIcon,
            itemShape,
            combatDefinition);
        definition.skillDefinitions = itemSkillDefinitions ??
            System.Array.Empty<CombatSystem.SkillDefinitionSO>();
        return definition;
    }
}
