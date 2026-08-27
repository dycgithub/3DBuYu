using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ShopAreaWeight
{
    [Min(1)] public int Area;
    [Min(0f)] public float Weight;
}

/// <summary>
/// 物品目录
/// 集中管理所有 ItemDefinition,供商店 / 存档恢复 / 程序化生成按 id 查询。
/// 未配置资产时使用 Default(空目录,零配置不崩溃)。
/// </summary>
[CreateAssetMenu(fileName = "ItemCatalog", menuName = "Inventory/Item Catalog")]
public class ItemCatalog : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> items = new();

    [Header("商店面积权重")]
    [SerializeField, Min(0f)] private float defaultShopAreaWeight = 1f;
    [SerializeField] private List<ShopAreaWeight> shopAreaWeights = new();

    private static ItemCatalog _default;

    /// <summary>全局默认目录(空,零配置可用)。</summary>
    public static ItemCatalog Default
    {
        get
        {
            if (_default == null) _default = CreateInstance<ItemCatalog>();
            return _default;
        }
    }

    /// <summary>目录内全部定义(只读)。</summary>
    public IReadOnlyList<ItemDefinition> Items => items;

    /// <summary>
    /// 获取商店包围盒面积权重。未单独配置的面积使用默认权重。
    /// </summary>
    public float GetShopAreaWeight(int area)
    {
        if (area <= 0) return 0f;

        if (shopAreaWeights != null)
        {
            for (int i = 0; i < shopAreaWeights.Count; i++)
            {
                if (shopAreaWeights[i].Area == area)
                    return Mathf.Max(0f, shopAreaWeights[i].Weight);
            }
        }

        return Mathf.Max(0f, defaultShopAreaWeight);
    }

    /// <summary>按 id 查询定义,未找到返回 null。</summary>
    public ItemDefinition GetById(string id)
    {
        if (items == null || string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item != null && item.Id == id) return item;
        }

        return null;
    }

    /// <summary>运行时添加定义(测试 / 程序化注册),重复添加自动忽略。</summary>
    public void Add(ItemDefinition definition)
    {
        if (definition == null || items.Contains(definition)) return;
        items.Add(definition);
    }
}
