using System.Collections.Generic;
using Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// - 逐格形状渲染:每个形状点 = 一个小方块,T形显示成T形,旋转时方块跟着转
/// - 拖拽/旋转/放置/跨网格:数据走 TetrisItemVM,松手 RaycastAll 找目标网格
/// 外壳 Image 作为拖拽热区(可放底图,不放则只有形状方块可见)。
/// </summary>
[RequireComponent(typeof(Image))]
public class ItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private RectTransform rect;
    private bool isDragging;
    private int originCol, originRow;
    private Dir originDir;   // 拖拽前的方向快照,回退时用于还原
    private ItemGhostView ghost;
    private Camera pointerCamera;
    private GridView lastHighlightGrid;
    private List<Image> cells = new();
    private ItemOutlineView outline;

    public ItemVM ItemVM { get; private set; }
    public GridView OwnerGrid { get; private set; }

    public RectTransform Rect => rect;

    [Inject] private IInventoryDragState _inventory;
    [Inject] private IInventorySelectionState _selection;
    [Inject] private IItemTooltipService _tooltip;
    [Inject] private IShopService _shop;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        outline = GetComponentInChildren<ItemOutlineView>(true);
    }

    public void Init(GridView grid, ItemVM item, int col, int row)
    {
        OwnerGrid = grid;
        ItemVM = item;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = grid.SizeFor(item);
        EnsureOutline();
        RebuildCells();
        PlaceAt(col, row);
    }

    /// <summary>跨网格放置成功后更新归属。</summary>
    public void SetOwner(GridView grid)
    {
        OwnerGrid = grid;
        _tooltip?.RefreshSelectedTooltip();
    }

    /// <summary>把 UI 放到某格(左上角对齐该格)。</summary>
    public void PlaceAt(int col, int row)
    {
        rect.anchoredPosition = OwnerGrid.GridToAnchoredPos(col, row);
    }

    /// <summary>顺时针旋转 90°。</summary>
    public void Rotate()
    {
        if (!isDragging || ghost == null) return;

        ItemVM.Rotate();
        // 旋转会改变包围盒宽高(如 1×2 ↔ 2×1),必须同步刷新外壳尺寸,
        // 否则点击/拖拽热区(外壳 Image)仍是旋转前包围盒,点击判定错位
        rect.sizeDelta = OwnerGrid.SizeFor(ItemVM);
        RebuildCells(); // 物品方块和轮廓跟着转
        ghost.RebuildCells(OwnerGrid, ItemVM); // 影子方块跟着转
        RefreshGhost(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
        _tooltip?.RefreshSelectedTooltip();
    }

    /// <summary>按当前旋转重建形状方块和非矩形轮廓。</summary>
    public void RebuildCells()
    {
        if (ItemVM == null || OwnerGrid == null)
            return;

        ClearCells();
        cells.AddRange(BuildCells(rect, ItemVM.CoordinateSet, ItemVM.RotationOffset,
            OwnerGrid.CellSize, OwnerGrid.Step,
            ResolveSprite(ItemVM, OwnerGrid.CellSprite),
            ResolveColor(ItemVM, OwnerGrid.CellColor)));

        EnsureOutline();
        outline.Rebuild(ItemVM.CoordinateSet, ItemVM.RotationOffset, OwnerGrid.CellSize, OwnerGrid.Step);
        outline.SetSelected(_selection != null && _selection.SelectedItem == this);
        outline.transform.SetAsLastSibling();
    }

    /// <summary>物品外观 sprite:定义图标优先,回退网格底图(零配置可用)。</summary>
    public static Sprite ResolveSprite(ItemVM item, Sprite fallback)
        => item?.Definition != null && item.Definition.Icon != null ? item.Definition.Icon : fallback;

    /// <summary>物品外观颜色:定义颜色优先,回退网格色。</summary>
    public static Color ResolveColor(ItemVM item, Color fallback)
        => item?.Definition != null ? item.Definition.Color : fallback;

    /// <summary>设置当前物品的选中轮廓样式。</summary>
    public void SetSelected(bool selected)
    {
        EnsureOutline();
        outline.SetSelected(selected);
        outline.transform.SetAsLastSibling();
    }

    /// <summary>控制物品轮廓层是否可见。</summary>
    public void SetOutlineVisible(bool visible)
    {
        EnsureOutline();
        outline.SetVisible(visible);
    }

    private void EnsureOutline()
    {
        if (outline != null)
            return;

        var go = new GameObject("ItemOutline", typeof(RectTransform), typeof(ItemOutlineView));
        go.transform.SetParent(rect, false);
        outline = go.GetComponent<ItemOutlineView>();
    }

    private void ClearCells()
    {
        for (int i = 0; i < cells.Count; i++)
            if (cells[i] != null)
                Destroy(cells[i].gameObject);
        cells.Clear();
    }

    /// <summary>
    /// 公共方块生成(物品与影子共用):每个形状点 = 一个 Image。
    /// 位置相对 parent 左上角;sprite 为空时用自建白色方块(零配置可用)。
    /// </summary>
    public static List<Image> BuildCells(RectTransform parent, IReadOnlyList<Vector2Int> points,
        Vector2Int rotationOffset,
        float cellSize, float step, Sprite sprite, Color color)
    {
        if (sprite == null) sprite = GridUtilities.WhiteSprite;

        var list = new List<Image>(points.Count);
        foreach (var p in points)
        {
            int cx = p.x + rotationOffset.x;
            int cy = p.y + rotationOffset.y;

            var go = new GameObject("Cell", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = new Vector2(cx * step, -cy * step);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false; // 不拦截鼠标(拖拽由外壳 Image 接收)
            list.Add(img);
        }

        return list;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _selection?.Select(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _selection?.Select(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _tooltip?.ShowHover(this, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        _tooltip?.MoveHover(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltip?.HideHover(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _selection?.Select(this);
        _tooltip?.HideHover(this);
        isDragging = true;
        originCol = ItemVM.LocalGridCoordinate.x;
        originRow = ItemVM.LocalGridCoordinate.y;
        originDir = ItemVM.Direction;
        pointerCamera = eventData.pressEventCamera;

        if (OwnerGrid == null || !OwnerGrid.DetachItem(ItemVM))
        {
            isDragging = false;
            return;
        }

        var go = new GameObject("ItemGhost", typeof(Image));
        go.transform.SetParent(OwnerGrid.ItemContainer, false);
        ghost = go.AddComponent<ItemGhostView>();
        ghost.Init(OwnerGrid, ItemVM);

        _inventory.SetDragging(this);
        RefreshGhost(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        RefreshGhost(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;

        _inventory.SetDragging(null);

        bool placed = false;
        GridView target = FindTargetGrid(eventData.position);
        if (target != null &&
            target.ScreenToGrid(eventData.position, pointerCamera, out int targetCol, out int targetRow) &&
            target.CanPlace(ItemVM, targetCol, targetRow))
        {
            placed = TryCompleteDrop(target, targetCol, targetRow);
        }

        if (!placed)
        {
            // 回退:先还原拖拽前的方向,再放回原坐标,避免"旋转后回退"导致错位/重叠/丢物品
            ItemVM.SetDirection(originDir);
            rect.sizeDelta = OwnerGrid.SizeFor(ItemVM);
            RebuildCells();
            if (!OwnerGrid.PlaceItem(this, originCol, originRow))
                Debug.LogWarning("[ItemView] 回退放置失败,物品可能丢失", this);
        }

        ClearHighLight();
        Destroy(ghost.gameObject);
        ghost = null;
        _tooltip?.RefreshSelectedTooltip();
    }

    private void OnDestroy()
    {
        _tooltip?.HideHover(this);
        if (_selection != null && _selection.SelectedItem == this)
            _selection.ClearSelection();
    }

    /// <summary>
    /// 跨网格放置 + 商店交易路由(按源/目标网格类型决定是否购买):
    /// - Shop→其他:购买,扣费成功后放置;积分不足返回 false,由调用方回滚到商店原位。
    /// - 其他→Shop:放回商品并退还购买价格。
    /// - 同类型(Shop→Shop / 其他→其他):直接放置,不产生交易。
    /// 经济操作收拢在 IShopService(ShopManager),此处只做路由。
    /// </summary>
    private bool TryCompleteDrop(GridView target, int col, int row)
    {
        bool fromShop = OwnerGrid.GridType == GridType.Shop;
        bool toShop = target.GridType == GridType.Shop;

        // 无所有权转移:直接放置,不产生交易
        if (fromShop == toShop)
            return target.PlaceItem(this, col, row);

        // 其他网格 → Shop:放回商品后退还购买价格。
        if (toShop)
        {
            if (_shop == null || !target.PlaceItem(this, col, row))
                return false;

            _shop.Refund(ItemVM);
            return true;
        }

        // 购买:Shop → 其他。扣费失败(积分不足)则不放置,由调用方回滚到原网格。
        if (_shop == null || !_shop.TryPurchase(ItemVM))
            return false;

        // 目标位置已在松手前验证；若发生极端并发变化,购买费用立即回退。
        if (target.PlaceItem(this, col, row))
            return true;

        _shop.Refund(ItemVM);
        return false;
    }

    private void RefreshGhost(Vector2 screenPos)
    {
        GridView target = FindTargetGrid(screenPos);
        if (target != null && target.ScreenToGrid(screenPos, pointerCamera, out int col, out int row))
        {
            if (lastHighlightGrid != null && lastHighlightGrid != target) lastHighlightGrid.HideHighlight();

            bool valid = target.EvaluatePlacement(ItemVM, col, row, out _);
            target.ShowHighlight(ItemVM, col, row, valid);
            lastHighlightGrid = target;

            // 影子:在自己网格内吸附,别处跟随鼠标
            if (target == OwnerGrid)
            {
                // 回到源网格物品层(吸附用局部坐标,父必须正确)
                if (ghost.transform.parent != OwnerGrid.ItemContainer)
                    ghost.transform.SetParent(OwnerGrid.ItemContainer, false);
                ghost.SetAnchoredPos(OwnerGrid.GridToAnchoredPos(col, row));
            }
            else
            {
                // 跨网格:切换父对象到目标网格物品层,避免被目标面板遮挡;位置由 Follow 立即纠正
                if (ghost.transform.parent != target.ItemContainer)
                    ghost.transform.SetParent(target.ItemContainer, false);
                ghost.Follow(screenPos);
            }
        }
        else
        {
            ghost.Follow(screenPos);
            ClearHighLight();
        }
    }

    private void ClearHighLight()
    {
        if (lastHighlightGrid != null)
        {
            lastHighlightGrid.HideHighlight();
            lastHighlightGrid = null;
        }
    }

    /// <summary>松手时查一次鼠标下的网格(不做每帧开销)。</summary>
    private GridView FindTargetGrid(Vector2 screenPos)
    {
        if (EventSystem.current == null) return null;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject == null) continue;
            var grid = results[i].gameObject.GetComponentInParent<GridView>();
            if (grid != null) return grid;
        }

        return null;
    }
}
