using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 放置高亮(参考 Cholopol TIS 的 InventoryHighlight):
/// 拖拽时按形状渲染半透明色块:绿=可放置,红=不可放置。
/// 色块运行时创建 + 对象池,零配置可用。
/// </summary>
public class InventoryHighLight : MonoBehaviour
{
    private readonly Stack<Image> pool = new();   // 空闲色块
    private readonly List<Image> active = new();  // 正在显示的色块
    private Sprite whiteSprite;

    private void Awake()
    {
        whiteSprite = GridUtilities.WhiteSprite; 
    }

    /// <summary>
    /// 在网格上显示高亮。
    /// col/row = 锚点格子;valid = 是否可放置(决定绿/红);越界格子不渲染。
    /// </summary>
    public void Show(GridView grid, ItemVM item, int col, int row, bool valid)
    {
        Clear();
        
        Color color = valid ? grid.PlacementConfig.ValidColor:grid.PlacementConfig.InvalidColor;
        float cellSize = grid.CellSize;

        foreach (var p in item.CoordinateSet)
        {
            int gx = col + p.x + item.RotationOffset.x;
            int gy = row + p.y + item.RotationOffset.y;
            
            if (gx < 0 || gy < 0 || gx >= grid.GridVM.Width || gy >= grid.GridVM.Height) continue;
            Image image = GetTile();
            var rt=image.rectTransform;
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = grid.GridToAnchoredPos(gx, gy);
            image.color = color;
            active.Add(image);
        }
    }

    /// <summary>清除所有高亮色块(回收进池)。</summary>
    public void Clear()
    {
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] != null)
            {
                active[i].gameObject.SetActive(false);
                pool.Push(active[i]);
            }
        }
        active.Clear();
    }
    
    private Image GetTile()
    {
        if (pool.Count > 0)
        {
            var _image = pool.Pop();
            _image.gameObject.SetActive(true);
            return _image;
        }
        
        var go=new GameObject("HighlightTile",typeof(Image));
        var image = go.GetComponent<Image>();
        image.sprite = whiteSprite;
        image.raycastTarget = false;
        return image;
    }
}
