using UnityEngine;
using CombatSystem;

/// <summary>
/// 物品定义(对齐 Cholopol TIS 的 ItemDetails,仅身份+形状):
/// 一个资产 = 一种物品类型;多个 ItemDefinition 即"多种 item"。
/// 价格/属性/技能等扩展字段待玩法确定后再加(遵循开闭原则,以扩展方式补充)。
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
    [SerializeField] private Color color = Color.white;

    [Header("网格形状")]
    [SerializeField] private ItemShape shape = ItemShape.Single;

    [Header("商店价格")]
    [SerializeField] private int price;

    [Header("战斗配置")]
    [SerializeField] private CombatSystem.ItemCombatDefinition combatDefinition;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public Color Color => color;
    public ItemShape Shape => shape;

    /// <summary>商店售价(积分,Points)。0 = 免费商品。</summary>
    public int Price => price;
    public CombatSystem.ItemCombatDefinition CombatDefinition => combatDefinition;

    /// <summary>
    /// 运行时创建临时定义:测试窗口 / 商店程序化生成 / 无资产环境使用。
    /// 正式配置请用 CreateAssetMenu 创建资产资产。
    /// </summary>
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
}
