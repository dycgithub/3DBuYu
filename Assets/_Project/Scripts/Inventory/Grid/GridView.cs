using System;
using System.Collections.Generic;
using Services;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

[RequireComponent(typeof(GridInteract))]
public class GridView : MonoBehaviour
{
    private static readonly List<GridView> _activeGrids = new();

    public static IReadOnlyList<GridView> ActiveGrids => _activeGrids;
    public static event Action<GridView> Registered;
    public static event Action<GridView> Unregistered;

    [Header("网格")] 
    [SerializeField] private int width = 5;
    [SerializeField] private int height = 5;
    [SerializeField] private GridType gridType = GridType.StorageForShop;
    
    [Header("UI")]
    [SerializeField] private RectTransform gridContainer;
    [SerializeField] private RectTransform itemContainer;
    [SerializeField] private RectTransform cellPrefab;
    [SerializeField] private RectTransform itemPrefab;
    [SerializeField] private Sprite cellSprite;
    [SerializeField] private Color cellColor=new Color(0.75f,0.85f,1f);
    [SerializeField] private InventoryPlacementConfig placementConfig;
    [SerializeField] private ItemShapeSet shapeSet;
    private const int UnassignedTransmitterIndex = -1;

    [Header("战斗绑定")]
    private int _transmitterIndex = UnassignedTransmitterIndex;
    private InventoryHighLight highlight;
    
    /// <summary>网格数据(VM),所有判定都走这里。</summary>
    public GridVM GridVM { get; private set; }
    /// <summary>网格分类(Shop/Storage/Equipment)。</summary>
    public GridType GridType => gridType;

    /// <summary>
    /// CentralCore 分配的发射器索引；未分配的发射器背包为 -1。
    /// </summary>
    public int TransmitterIndex => _transmitterIndex;

    /// <summary>网格内物品数量(数据层,去重:一个物品占多格只计一次)。</summary>
    public int ItemCount => GridVM != null ? GridVM.ItemCount : 0;
    /// <summary>网格内全部物品(去重)。</summary>
    public System.Collections.Generic.IEnumerable<ItemVM> Items => GridVM != null ? GridVM.Items : System.Array.Empty<ItemVM>();
    /// <summary>物品层(跨网格拖放时物品会 reparent 到这里)。</summary>
    public RectTransform ItemContainer => itemContainer;
    
    [Header("Grid参数")]
    [SerializeField] private float cellSize = 40f;             
    [SerializeField] private float cellSpacing = 10f;
    
    /// <summary>相邻格子中心步长 = 格宽 + 间距。</summary>
    public float Step => cellSize + cellSpacing;
    public float CellSize => cellSize;
    public Sprite CellSprite => cellSprite;
    public Color CellColor => cellColor;
    public InventoryPlacementConfig PlacementConfig => placementConfig != null ? placementConfig : InventoryPlacementConfig.Default;
    /// <summary>形状库(优先场景资产,未配置用全局默认库)。</summary>
    public ItemShapeSet ShapeSet => shapeSet != null ? shapeSet : ItemShapeSet.Default;

    public event Action<GridView> ItemsChanged;

    /// <summary>
    /// 由 CentralCore 按端口配置顺序分配发射器索引。
    /// </summary>
    /// <param name="transmitterIndex">对应 CentralSO.Transmitters 的零基索引。</param>
    /// <returns>网格是发射器背包且索引有效时返回 true。</returns>
    public bool AssignTransmitter(int transmitterIndex)
    {
        if (gridType != GridType.TransmitterBackpack)
        {
            Debug.LogWarning($"[GridView] {name}: 只有 TransmitterBackpack 可以绑定发射器。", this);
            return false;
        }

        if (transmitterIndex < 0)
        {
            Debug.LogWarning($"[GridView] {name}: 发射器索引必须为非负数。", this);
            return false;
        }

        if (_transmitterIndex == transmitterIndex)
            return true;

        _transmitterIndex = transmitterIndex;
        ItemsChanged?.Invoke(this);
        return true;
    }

    [Inject] private IObjectResolver _resolver;
    [Inject] private IShopService _shop;
    [Inject] private IInventoryTransferStorage _inventoryTransferStorage;

    private void OnEnable()
    {
        if (_activeGrids.Contains(this))
            return;
        _activeGrids.Add(this);
        Registered?.Invoke(this);
    }

    private void OnDisable()
    {
        if (!_activeGrids.Remove(this))
            return;
        Unregistered?.Invoke(this);
    }

    private void Start()
    {
        EnsureGridVM();
        SetUpLayout();
        GenerateCells();
        SetupPlacementHighlight();

        if (gridType == GridType.StorageForPlay)
        {
            if (_inventoryTransferStorage == null)
            {
                Debug.LogWarning("[GridView] StorageForPlay 未注入临时物品存储,无法恢复 UI 物品", this);
            }
            else if (_inventoryTransferStorage.HasPendingItems &&
                     RestoreItems(_inventoryTransferStorage.PendingItems))
            {
                _inventoryTransferStorage.Clear();
            }
        }

        // 商店网格:进入场景时清空并填充商品(商店行为绑定在 GridType 属性上,零额外组件)
        if (gridType == GridType.Shop)
        {
            _shop?.RefreshShop(this);
        }
    }

    /// <summary>确保网格数据已创建(编辑模式调用时 Start 未执行,GridVM 为 null)。</summary>
    public void EnsureGridVM()
    {
        if (GridVM == null) GridVM = new GridVM(width, height, gridType);
    }

    private void SetUpLayout()
    {
        var layout =gridContainer.GetComponent<GridLayoutGroup>();
        layout.cellSize=new Vector2(cellSize,cellSize);
        layout.spacing=new Vector2(cellSpacing,cellSpacing);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = width;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.padding = new RectOffset(0,0,0,0);
        
        gridContainer.anchorMin = Vector2.zero;
        gridContainer.anchorMax = Vector2.one;
        gridContainer.offsetMin = Vector2.zero;
        gridContainer.offsetMax = Vector2.zero;
        
        itemContainer.anchorMin = Vector2.zero;
        itemContainer.anchorMax = Vector2.one;
        itemContainer.offsetMin = Vector2.zero;
        itemContainer.offsetMax = Vector2.zero;

        var le = itemContainer.GetComponent<LayoutElement>();
        if (le == null) le = itemContainer.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
    }

    private void GenerateCells()
    {
        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                RectTransform cell = Instantiate(cellPrefab, gridContainer);
                cell.name = $"Cell_{c}_{r}";
            }
        }
    }
    /// <summary>创建放置高亮层(渲染在物品下层)。</summary>
    private void SetupPlacementHighlight()
    {
        var go = new GameObject("PlacementHighlight", typeof(RectTransform), typeof(InventoryHighLight));
        go.transform.SetParent(itemContainer, false);
        go.transform.SetAsFirstSibling(); // 高亮在物品下层

        var rectTransform = go.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;

        highlight = go.GetComponent<InventoryHighLight>();
    }
    
    /// <summary>拖拽时显示高亮(绿=可放 / 红=不可放)。</summary>
    public void ShowHighlight(ItemVM item, int col, int row, bool valid)
    {
        highlight.Show(this, item, col, row, valid);
    }

    public void HideHighlight() => highlight.Clear();

    /// <summary>屏幕坐标 → 网格格子(列, 行)。鼠标不在容器内返回 false。</summary>
    public bool ScreenToGrid(Vector2 screenPos, Camera cam, out int col, out int row)
    {
        col = row = -1;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridContainer, screenPos, cam, out var localPoint))
            return false;

        Rect rect = gridContainer.rect;

        // 以容器左上角为原点:向右为 x(列),向下为 y(行)
        float fromLeft=localPoint.x+gridContainer.pivot.x*rect.width;
        float fromTop = -(localPoint.y - (1f - gridContainer.pivot.y) * rect.height);

        float step = Step;
        
        col=Mathf.FloorToInt(fromLeft/step);
        row=Mathf.FloorToInt(fromTop/step);
        return col >= 0 && row >= 0;
    }
    
    /// <summary>格子左上角 → 物品层中的 anchoredPosition。</summary>
    public Vector2 GridToAnchoredPos(int col, int row) => new Vector2(col * Step, -row * Step);

    /// <summary>物品当前方向下的像素尺寸(含间距)。</summary>
    public Vector2 SizeFor(ItemVM item)
        => new Vector2(item.Width * cellSize + (item.Width - 1) * cellSpacing,
            item.Height * cellSize + (item.Height - 1) * cellSpacing);
    
    public bool CanPlace(ItemVM item, int col, int row) => GridVM.CanPlace(item, col, row);
    
    /// <summary>规则层判定放置合法性(绿/红高亮的依据)。</summary>
    public bool EvaluatePlacement(ItemVM item, int col, int row, out PlacementBlockReason reason)
        => PlacementConfig.Evaluate(GridVM, item, col, row, out reason);

    /// <summary>
    /// 放置物品:写数据(GridVM.Place) + 移动 UI。
    /// 若物品来自其他网格(跨网格拖放),自动 reparent 并更新归属。
    /// </summary>
    public bool PlaceItem(ItemView view, int col, int row)
    {
        if(!GridVM.CanPlace(view.ItemVM, col, row)) return false;

        if (view.OwnerGrid != this)
        {
            view.transform.SetParent(itemContainer, false);
            view.Rect.sizeDelta = SizeFor(view.ItemVM);
            view.SetOwner(this);
            view.RebuildCells();
        }
        
        GridVM.Place(view.ItemVM, col, row);
        view.PlaceAt(col, row);
        ItemsChanged?.Invoke(this);
        return true;
    }

    public ItemView SpawnItem(ItemVM item, int col, int row)
    {
        // 先写占用数据,失败(越界/占用)则拒绝生成
        if (!GridVM.Place(item, col, row))
        {
            Debug.LogWarning($"无法在 ({col},{row}) 生成物品:越界或格子被占用");
            return null;
        }

        // 编辑模式下 _resolver 可能为 null(ProjectLifetimeScope 未构建),回退原生实例化
        RectTransform go = _resolver != null
            ? _resolver.Instantiate(itemPrefab, itemContainer)
            : (RectTransform)Instantiate(itemPrefab, itemContainer);
        var view = go.GetComponent<ItemView>();
        if (view == null)
        {
            view = go.AddComponent<ItemView>();
            if (_resolver != null) _resolver.Inject(view);
        }
        view.Init(this, item, col, row);
        ItemsChanged?.Invoke(this);
        return view;
    }

    /// <summary>按形状枚举生成物品(测试工具入口;正式流程走 SpawnItem(ItemDefinition))。</summary>
    public ItemView SpawnItem(ItemShape shape, int col, int row)
    {
        EnsureGridVM();
        return SpawnItem(new ItemVM(ShapeSet.GetPoints(shape)), col, row);
    }

    /// <summary>按物品定义生成(主入口,目录/商店/存档恢复均调用此方法)。</summary>
    public ItemView SpawnItem(ItemDefinition definition, int col, int row)
    {
        EnsureGridVM();
        return SpawnItem(new ItemVM(definition, ShapeSet), col, row);
    }

    /// <summary>
    /// 按跨场景快照重建网格物品。
    /// 先在临时网格中验证完整布局,确认全部合法后再替换目标网格内容,避免只恢复一部分。
    /// </summary>
    public bool RestoreItems(IReadOnlyList<InventoryItemSnapshot> snapshots)
    {
        EnsureGridVM();

        if (snapshots == null || snapshots.Count == 0)
        {
            ClearAll();
            return true;
        }

        var items = new List<ItemVM>(snapshots.Count);
        var validationGrid = new GridVM(GridVM.Width, GridVM.Height, GridVM.GridType);

        for (int i = 0; i < snapshots.Count; i++)
        {
            InventoryItemSnapshot snapshot = snapshots[i];
            if (snapshot == null)
            {
                Debug.LogWarning($"[GridView] 恢复 StorageForPlay 失败:第 {i} 个物品快照为空", this);
                return false;
            }

            ItemVM item = snapshot.CreateItem();
            Vector2Int coordinate = snapshot.LocalGridCoordinate;
            if (!validationGrid.Place(item, coordinate.x, coordinate.y))
            {
                Debug.LogWarning(
                    $"[GridView] 恢复 StorageForPlay 失败:物品 {i} 无法放置在 ({coordinate.x},{coordinate.y})",
                    this);
                return false;
            }

            items.Add(item);
        }

        ClearAll();
        for (int i = 0; i < items.Count; i++)
        {
            Vector2Int coordinate = snapshots[i].LocalGridCoordinate;
            if (SpawnItem(items[i], coordinate.x, coordinate.y) == null)
            {
                ClearAll();
                Debug.LogWarning($"[GridView] 恢复 StorageForPlay 失败:第 {i} 个物品生成视图失败", this);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 清空网格全部物品(数据 + 视图层销毁)。
    /// 用于商店刷新等“整格重建”场景;不影响高亮层与格子底图。
    /// </summary>
    public void ClearAll()
    {
        if (GridVM == null) return;

        var all = new List<ItemVM>(GridVM.Items);
        foreach (var item in all) GridVM.Remove(item);

        if (itemContainer != null)
        {
            var views = itemContainer.GetComponentsInChildren<ItemView>();
            for (int i = 0; i < views.Length; i++)
                Destroy(views[i].gameObject);
        }

        ItemsChanged?.Invoke(this);
    }

    public bool RemoveItem(ItemVM item)
    {
        if (!DetachItem(item))
            return false;

        if (itemContainer != null)
        {
            ItemView[] views = itemContainer.GetComponentsInChildren<ItemView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i].ItemVM != item)
                    continue;

                Destroy(views[i].gameObject);
                break;
            }
        }

        return true;
    }

    /// <summary>暂时从数据网格移除物品，供拖拽回退或跨网格放置使用。</summary>
    public bool DetachItem(ItemVM item)
    {
        if (GridVM == null || item == null || !GridVM.Contains(item))
            return false;

        GridVM.Remove(item);
        ItemsChanged?.Invoke(this);
        return true;
    }
}
