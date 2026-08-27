namespace Services
{
    public interface IInventoryDragState
    {
        GridView HoveredGrid { get; }
        ItemView DraggingItem { get; }
        void SetHoveredGrid(GridView grid);
        void ClearHoveredGrid();
        void SetDragging(ItemView item);
    }
}