using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

public partial class GridView
{
    /// <summary>先验证并生成全部新视图，再授权扣费并替换旧内容；准备失败不碰旧网格。</summary>
    /// <param name="snapshots">要完整替换的商品快照。</param>
    /// <param name="authorize">准备成功后调用一次，返回 false 则丢弃新布局。</param>
    /// <returns>扣费授权及完整替换均成功时返回 true。</returns>
    public bool TryReplaceItems(IReadOnlyList<InventoryItemSnapshot> snapshots, Func<bool> authorize)
    {
        EnsureGridVM();
        if (snapshots == null || snapshots.Count == 0 || itemPrefab == null ||
            itemContainer == null || authorize == null)
        {
            return false;
        }

        var replacement = new GridVM(GridVM.Width, GridVM.Height, GridVM.GridType);
        var items = new List<ItemVM>(snapshots.Count);
        foreach (InventoryItemSnapshot snapshot in snapshots)
        {
            if (snapshot == null)
            {
                return false;
            }
            ItemVM item = snapshot.CreateItem();
            Vector2Int position = snapshot.LocalGridCoordinate;
            if (!replacement.Place(item, position.x, position.y))
            {
                return false;
            }
            items.Add(item);
        }

        // 暂存层不属于 ItemContainer，清理旧商品时不会把新视图一起销毁。
        var staging = new GameObject("PreparedItems", typeof(RectTransform), typeof(CanvasGroup));
        staging.transform.SetParent(transform, false);
        CanvasGroup group = staging.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        var views = new List<ItemView>(items.Count);
        try
        {
            try
            {
                foreach (ItemVM item in items)
                {
                    RectTransform instance = _resolver != null
                        ? _resolver.Instantiate(itemPrefab, staging.transform)
                        : Instantiate(itemPrefab, staging.transform);
                    ItemView view = instance.GetComponent<ItemView>();
                    if (view == null)
                    {
                        return false;
                    }
                    Vector2Int coordinate = item.LocalGridCoordinate;
                    view.Init(this, item, coordinate.x, coordinate.y);
                    views.Add(view);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GridView] 准备商品视图失败: {exception.Message}", this);
                return false;
            }

            if (!authorize())
            {
                return false;
            }

            ItemView[] oldViews = itemContainer.GetComponentsInChildren<ItemView>(true);
            foreach (ItemView old in oldViews)
            {
                old.CancelDragForGridClear();
                old.gameObject.SetActive(false);
            }
            GridVM = replacement;
            foreach (ItemView view in views)
            {
                view.transform.SetParent(itemContainer, false);
            }
            foreach (ItemView old in oldViews)
            {
                DestroyReplacementObject(old.gameObject);
            }
            ItemsChanged?.Invoke(this);
            return true;
        }
        finally
        {
            staging.SetActive(false);
            DestroyReplacementObject(staging);
        }
    }

    /// <summary>在运行模式延迟销毁，在编辑器验证中立即销毁，防止暂存层残留。</summary>
    private static void DestroyReplacementObject(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
