using System.Collections.Generic;
using Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// - 逐格形状渲染:每个形状点 = 一个小方块,T形显示成T形,旋转时方块跟着转
/// - 拖拽/旋转/放置/跨网格:数据走 TetrisItemVM,拖拽 Raycast 复用指针数据与结果列表
/// - 指针悬停和选中:把交互状态转发给 ItemOutlineView,由轮廓组件统一绘制
/// - 指针按压:把按下/抬起表现转发给 ItemPressEffect,不改变网格定位
/// 外壳 Image 作为拖拽热区(可放底图,不放则只有形状方块可见)。
/// </summary>
[RequireComponent(typeof(Image))]
public class ItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerEnterHandler,
    IPointerExitHandler, IPointerMoveHandler
{
    private RectTransform rect;
    private RectTransform visualRoot;
    private bool isDragging;
    private int originCol, originRow;
    private Dir originDir;   // 拖拽前的方向快照,回退时用于还原
    private ItemGhostView ghost;
    private Camera pointerCamera;
    private GridView lastHighlightGrid;
    private List<Image> cells = new();
    private readonly List<RaycastResult> _raycastResults = new();
    private EventSystem _cachedRaycastEventSystem;
    private PointerEventData _cachedPointerEventData;
    private ItemOutlineView outline;
    private ItemPressEffect pressEffect;
    private ItemMaskedVisual maskedVisual;

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
        pressEffect = GetComponent<ItemPressEffect>();
        visualRoot = pressEffect != null
            ? pressEffect.VisualRoot
            : transform.Find("VisualRoot") as RectTransform;
        if (visualRoot == null)
        {
            visualRoot = rect;
        }

        outline = GetComponentInChildren<ItemOutlineView>(true);
        maskedVisual = GetComponentInChildren<ItemMaskedVisual>(true);
    }

    /// <summary>绑定物品和网格；编辑器准备商品时 Awake 未运行，先补齐组件缓存再绘制。</summary>
    public void Init(GridView grid, ItemVM item, int col, int row)
    {
        if (rect == null)
        {
            Awake();
        }
        OwnerGrid = grid;
        ItemVM = item;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = grid.SizeFor(item);
        ConfigureVisualRoot();
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

        ConfigureVisualRoot();
        ClearCells();
        if (!TryConfigureMaskedVisual())
        {
            cells.AddRange(BuildCells(visualRoot, ItemVM.CoordinateSet, ItemVM.RotationOffset,
                OwnerGrid.CellSize, OwnerGrid.Step,
                ResolveSprite(ItemVM, OwnerGrid.CellSprite),
                ResolveColor(ItemVM, OwnerGrid.CellColor)));
        }

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
        go.transform.SetParent(visualRoot, false);
        outline = go.GetComponent<ItemOutlineView>();
    }

    private void ConfigureVisualRoot()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.anchorMin = new Vector2(0f, 1f);
        visualRoot.anchorMax = new Vector2(0f, 1f);
        visualRoot.pivot = new Vector2(0f, 1f);
        visualRoot.anchoredPosition = Vector2.zero;
        visualRoot.sizeDelta = rect.sizeDelta;
    }

    private void ClearCells()
    {
        for (int i = 0; i < cells.Count; i++)
            if (cells[i] != null)
                Destroy(cells[i].gameObject);
        cells.Clear();
    }

    private bool TryConfigureMaskedVisual()
    {
        Sprite sprite = ItemVM?.Definition?.BoundingBoxImage;
        if (maskedVisual == null || sprite == null)
        {
            if (maskedVisual != null)
            {
                maskedVisual.gameObject.SetActive(false);
            }

            return false;
        }

        maskedVisual.gameObject.SetActive(true);
        Vector2 currentSize = OwnerGrid.SizeFor(ItemVM);
        (int width, int height) = GridUtilities.RotationHelper.GetBoundaryBox(ItemVM.BasePoints);
        float spacing = OwnerGrid.Step - OwnerGrid.CellSize;
        Vector2 artworkSize = new(
            width * OwnerGrid.CellSize + Mathf.Max(0, width - 1) * spacing,
            height * OwnerGrid.CellSize + Mathf.Max(0, height - 1) * spacing);
        maskedVisual.Configure(
            ItemVM.CoordinateSet,
            ItemVM.RotationOffset,
            OwnerGrid.CellSize,
            OwnerGrid.Step,
            sprite,
            ResolveColor(ItemVM, OwnerGrid.CellColor),
            currentSize,
            artworkSize,
            GridUtilities.RotationHelper.GetRotationAngle(ItemVM.Direction));
        return true;
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
        pressEffect ??= GetComponent<ItemPressEffect>();
        pressEffect?.Press();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressEffect ??= GetComponent<ItemPressEffect>();
        pressEffect?.Release();
    }

    /// <summary>组件停用时回滚未结束的拖拽事务并清理影子状态。</summary>
    private void OnDisable()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        RestoreDragOrigin();

        ClearHighLight();
        ClearGhost();
        _inventory?.SetDragging(null);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _selection?.Select(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);
        _tooltip?.ShowHover(this, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        _tooltip?.MoveHover(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
        _tooltip?.HideHover(this);
    }

    /// <summary>设置物品轮廓的悬停状态。</summary>
    private void SetHovered(bool hovered)
    {
        EnsureOutline();
        outline.SetHovered(hovered);
    }

    /// <summary>仅允许一个物品同时拖拽，避免装备顺序事务交叉。</summary>
    /// <param name="eventData">当前拖拽指针事件。</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_inventory?.DraggingItem != null && _inventory.DraggingItem != this)
        {
            return;
        }

        pressEffect?.Cancel();
        _selection?.Select(this);
        _tooltip?.HideHover(this);
        isDragging = true;
        originCol = ItemVM.LocalGridCoordinate.x;
        originRow = ItemVM.LocalGridCoordinate.y;
        originDir = ItemVM.Direction;
        pointerCamera = eventData.pressEventCamera;

        _inventory.SetDragging(this);
        if (OwnerGrid == null || !OwnerGrid.DetachItem(ItemVM, false))
        {
            isDragging = false;
            _inventory.SetDragging(null);
            return;
        }

        var go = new GameObject("ItemGhost", typeof(Image));
        go.transform.SetParent(OwnerGrid.ItemContainer, false);
        ghost = go.AddComponent<ItemGhostView>();
        ghost.Init(OwnerGrid, ItemVM);

        RefreshGhost(eventData.position, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        RefreshGhost(eventData.position, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;

        GridView sourceGrid = OwnerGrid;

        _inventory.SetDragging(null);

        bool placed = false;
        GridView target = FindTargetGrid(eventData);
        if (target != null &&
            target.ScreenToGrid(eventData.position, pointerCamera, out int targetCol, out int targetRow) &&
            target.CanPlace(ItemVM, targetCol, targetRow))
        {
            placed = TryCompleteDrop(target, targetCol, targetRow);

            // 跨网格移动时目标网格已发事件，源网格需要补一次最终状态同步。
            if (placed && sourceGrid != null && sourceGrid != OwnerGrid)
                sourceGrid.NotifyItemsChanged();
        }

        if (!placed)
        {
            // 回滚必须使用暂停时保存的顺序，避免失败拖拽改变最新装备道具。
            if (!RestoreDragOrigin())
            {
                Debug.LogWarning("[ItemView] 回退放置失败,物品可能丢失", this);
            }
        }

        ClearHighLight();
        Destroy(ghost.gameObject);
        ghost = null;
        _tooltip?.RefreshSelectedTooltip();
    }

    private void OnDestroy()
    {
        ClearGhost();
        if (_inventory?.DraggingItem == this)
        {
            _inventory.SetDragging(null);
        }

        _tooltip?.HideHover(this);
        if (_selection != null && _selection.SelectedItem == this)
            _selection.ClearSelection();
    }

    /// <summary>销毁当前拖拽影子并清除其引用。</summary>
    private void ClearGhost()
    {
        if (ghost == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(ghost.gameObject);
        }
        else
        {
            DestroyImmediate(ghost.gameObject);
        }
        ghost = null;
    }

    /// <summary>网格整体清空时中止拖拽，防止延迟的 OnEndDrag 恢复旧物品。</summary>
    internal void CancelDragForGridClear()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        RestoreDragOrigin();
        ClearHighLight();
        ClearGhost();

        if (_inventory?.DraggingItem == this)
        {
            _inventory.SetDragging(null);
        }
    }

    /// <summary>检查当前拖拽是否指向指定目标网格。</summary>
    /// <param name="grid">待检查的目标网格。</param>
    /// <returns>指针或放置高亮位于该网格时返回 true。</returns>
    internal bool IsDraggingOver(GridView grid)
    {
        return isDragging && (lastHighlightGrid == grid || _inventory?.HoveredGrid == grid);
    }

    /// <summary>把暂停的网格物品恢复到拖拽开始时的位置和方向。</summary>
    /// <returns>无需恢复或成功放回时返回 true。</returns>
    private bool RestoreDragOrigin()
    {
        if (OwnerGrid == null || OwnerGrid.GridVM == null ||
            !OwnerGrid.GridVM.IsSuspended(ItemVM))
        {
            return true;
        }

        ItemVM.SetDirection(originDir);
        rect.sizeDelta = OwnerGrid.SizeFor(ItemVM);
        RebuildCells();
        return OwnerGrid.PlaceItem(this, originCol, originRow);
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

    /// <summary>按当前指针位置更新影子与目标网格高亮。</summary>
    /// <param name="screenPos">当前指针的屏幕坐标。</param>
    /// <param name="eventData">Unity 当前拖拽事件；非拖拽定位时为空。</param>
    private void RefreshGhost(Vector2 screenPos, PointerEventData eventData = null)
    {
        GridView target = eventData != null
            ? FindTargetGrid(eventData)
            : FindTargetGridAtPosition(screenPos);
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

    /// <summary>通过当前 UI 指针事件查找下方网格，并复用结果缓冲区。</summary>
    /// <param name="eventData">当前指针事件。</param>
    /// <returns>命中的网格；未命中时返回 null。</returns>
    private GridView FindTargetGrid(PointerEventData eventData)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || eventData == null)
        {
            return null;
        }

        _raycastResults.Clear();
        eventSystem.RaycastAll(eventData, _raycastResults);
        for (int index = 0; index < _raycastResults.Count; index++)
        {
            if (_raycastResults[index].gameObject == null)
            {
                continue;
            }

            GridView grid = _raycastResults[index].gameObject.GetComponentInParent<GridView>();
            if (grid != null)
            {
                return grid;
            }
        }

        return null;
    }

    /// <summary>为非拖拽调用复用指针数据并查找下方网格。</summary>
    /// <param name="screenPos">用于查询的屏幕坐标。</param>
    /// <returns>命中的网格；未命中时返回 null。</returns>
    private GridView FindTargetGridAtPosition(Vector2 screenPos)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return null;
        }

        if (_cachedRaycastEventSystem != eventSystem)
        {
            _cachedRaycastEventSystem = eventSystem;
            _cachedPointerEventData = new PointerEventData(eventSystem);
        }

        _cachedPointerEventData.position = screenPos;
        return FindTargetGrid(_cachedPointerEventData);
    }
}
