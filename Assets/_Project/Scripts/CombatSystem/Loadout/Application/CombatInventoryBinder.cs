using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace CombatSystem
{
    public sealed class CombatInventoryBinder : IStartable, ICombatItemConsumer, IDisposable
    {
        private readonly CentralLoadout _centralLoadout;
        private readonly TransmitterLoadout _transmitterLoadout;
        private readonly List<GridView> _boundGrids = new();

        public CombatInventoryBinder(CentralLoadout centralLoadout, TransmitterLoadout transmitterLoadout)
        {
            _centralLoadout = centralLoadout;
            _transmitterLoadout = transmitterLoadout;
        }

        public void Start()
        {
            GridView.Registered += BindGrid;
            GridView.Unregistered += UnbindGrid;
            for (int i = 0; i < GridView.ActiveGrids.Count; i++)
                BindGrid(GridView.ActiveGrids[i]);
        }

        public bool CanConsume(int itemInstanceId)
        {
            return FindItem(itemInstanceId, out _, out _);
        }

        public bool TryConsume(int itemInstanceId)
        {
            if (!FindItem(itemInstanceId, out GridView grid, out ItemVM item))
                return false;
            return grid.RemoveItem(item);
        }

        public void Dispose()
        {
            GridView.Registered -= BindGrid;
            GridView.Unregistered -= UnbindGrid;
            for (int i = 0; i < _boundGrids.Count; i++)
                _boundGrids[i].ItemsChanged -= HandleItemsChanged;
            _boundGrids.Clear();
        }

        private void BindGrid(GridView grid)
        {
            if (grid == null || _boundGrids.Contains(grid))
                return;
            _boundGrids.Add(grid);
            grid.ItemsChanged += HandleItemsChanged;
            HandleItemsChanged(grid);
        }

        private void UnbindGrid(GridView grid)
        {
            if (grid == null || !_boundGrids.Remove(grid))
                return;
            grid.ItemsChanged -= HandleItemsChanged;
        }

        private void HandleItemsChanged(GridView grid)
        {
            if (grid == null)
                return;

            if (grid.GridType == GridType.CentralBackpack)
            {
                _centralLoadout.SetItems(grid.Items);
                return;
            }

            if (grid.GridType != GridType.TransmitterBackpack)
                return;

            int transmitterIndex = TransmitterGridBinding.ResolveIndex(grid.TransmitterId);
            if (transmitterIndex < 0)
                transmitterIndex = TransmitterGridBinding.ResolveIndex(grid.name);
            if (transmitterIndex < 0 && grid.transform.parent != null)
                transmitterIndex = TransmitterGridBinding.ResolveIndex(grid.transform.parent.name);
            if (transmitterIndex >= 0)
                _transmitterLoadout.SetItems(transmitterIndex, grid.Items);
        }

        private bool FindItem(int itemInstanceId, out GridView grid, out ItemVM item)
        {
            for (int gridIndex = 0; gridIndex < _boundGrids.Count; gridIndex++)
            {
                GridView candidateGrid = _boundGrids[gridIndex];
                foreach (ItemVM candidate in candidateGrid.Items)
                {
                    if (candidate != null && candidate.InstanceId == itemInstanceId)
                    {
                        grid = candidateGrid;
                        item = candidate;
                        return true;
                    }
                }
            }

            grid = null;
            item = null;
            return false;
        }
    }
}
