using System;
using System.Collections.Generic;
using GameSystem;
using UnityEngine;
using VContainer.Unity;

namespace CombatSystem
{
    /// <summary>
    /// 将库存网格变化同步到战斗装备状态，并提供物品实例消费服务。
    /// </summary>
    public sealed class CombatLoadoutInventoryBinder : IStartable, ICombatItemConsumptionService, IDisposable
    {
        private readonly CentralSkillLoadout _centralSkillLoadout;
        private readonly TransmitterAttackLoadout _transmitterAttackLoadout;
        private readonly List<GridView> _boundGrids = new();
        private readonly Dictionary<GridView, string> _transmitterPortIds = new();
        private readonly GameManager _gameManager;
        private readonly ITransmitterGlobalEffectResetService _globalEffectReset;
        private readonly ICentralSkillEffectResetService _centralEffectReset;
        private bool _isConsumingSkillItem;

        /// <summary>
        /// 创建库存与战斗装备之间的绑定服务。
        /// </summary>
        /// <param name="centralSkillLoadout">接收中心背包技能卡的装备状态。</param>
        /// <param name="transmitterAttackLoadout">接收发射器背包攻击修正的装备状态。</param>
        public CombatLoadoutInventoryBinder(
            CentralSkillLoadout centralSkillLoadout,
            TransmitterAttackLoadout transmitterAttackLoadout,
            GameManager gameManager,
            ITransmitterGlobalEffectResetService globalEffectReset = null,
            ICentralSkillEffectResetService centralEffectReset = null)
        {
            _centralSkillLoadout = centralSkillLoadout;
            _transmitterAttackLoadout = transmitterAttackLoadout;
            _gameManager = gameManager;
            _globalEffectReset = globalEffectReset;
            _centralEffectReset = centralEffectReset;
        }

        /// <summary>
        /// 订阅库存网格注册事件，并绑定当前已存在的网格。
        /// </summary>
        public void Start()
        {
            GridView.Registered += BindGrid;
            GridView.Unregistered += UnbindGrid;
            if (_gameManager != null)
            {
                _gameManager.OnGameStarted += SynchronizeAllGrids;
            }
            for (int i = 0; i < GridView.ActiveGrids.Count; i++)
            {
                BindGrid(GridView.ActiveGrids[i]);
            }

            if (_gameManager != null && _gameManager.CurrentState == GameState.Playing)
            {
                SynchronizeAllGrids();
            }
        }

        /// <summary>
        /// 预检查指定物品实例是否存在于已绑定网格中。
        /// </summary>
        /// <param name="itemInstanceId">物品实例标识，而非物品定义标识。</param>
        /// <returns>找到至少一个匹配实例时返回 <c>true</c>。</returns>
        public bool CanConsumeItemInstance(int itemInstanceId)
        {
            return FindBoundItemByInstanceId(itemInstanceId, out _, out _);
        }

        /// <summary>
        /// 尝试从所属库存网格移除技能来源物品，并保留本次施放已提交的限时效果。
        /// </summary>
        /// <param name="itemInstanceId">要移除的物品实例标识。</param>
        /// <returns>实际从网格移除物品时返回 <c>true</c>。</returns>
        public bool TryConsumeItemInstance(int itemInstanceId)
        {
            if (!FindBoundItemByInstanceId(itemInstanceId, out GridView grid, out ItemVM item))
            {
                return false;
            }

            bool wasConsumingSkillItem = _isConsumingSkillItem;
            _isConsumingSkillItem = true;
            try
            {
                return grid.RemoveItem(item);
            }
            finally
            {
                _isConsumingSkillItem = wasConsumingSkillItem;
            }
        }

        /// <summary>
        /// 解除所有事件订阅并清空本局战斗装备状态。
        /// </summary>
        /// <remarks>装备状态对象由 ProjectLifetimeScope 持有，因此必须在本场景解绑时主动清理。</remarks>
        public void Dispose()
        {
            GridView.Registered -= BindGrid;
            GridView.Unregistered -= UnbindGrid;
            if (_gameManager != null)
            {
                _gameManager.OnGameStarted -= SynchronizeAllGrids;
            }
            for (int i = 0; i < _boundGrids.Count; i++)
            {
                _boundGrids[i].ItemsChanged -= SynchronizeLoadoutFromGrid;
            }
            _boundGrids.Clear();
            _transmitterPortIds.Clear();
            _centralSkillLoadout?.Clear();
            _transmitterAttackLoadout?.ClearAllItems();
            _globalEffectReset?.ClearAll();
            _centralEffectReset?.ClearPermanentEffects();
        }

        /// <summary>绑定一个库存网格并立即同步其当前物品。</summary>
        /// <param name="grid">要绑定的网格；为空或已绑定时忽略。</param>
        private void BindGrid(GridView grid)
        {
            if (grid == null || _boundGrids.Contains(grid))
            {
                return;
            }
            _boundGrids.Add(grid);
            grid.ItemsChanged += SynchronizeLoadoutFromGrid;
            SynchronizeLoadoutFromGrid(grid);
        }

        /// <summary>解除一个库存网格的物品变化订阅并清除该网格留下的装备投影。</summary>
        /// <param name="grid">要解除的网格；为空或未绑定时忽略。</param>
        private void UnbindGrid(GridView grid)
        {
            if (grid == null || !_boundGrids.Remove(grid))
            {
                return;
            }
            grid.ItemsChanged -= SynchronizeLoadoutFromGrid;

            if (grid.GridType == GridType.CentralBackpack)
            {
                _globalEffectReset?.ClearAll();
                _centralEffectReset?.ClearPermanentEffects();
                _centralSkillLoadout.ReplaceItems(Array.Empty<ItemVM>());
                return;
            }

            if (grid.GridType == GridType.TransmitterBackpack && !string.IsNullOrEmpty(grid.PortId))
            {
                _transmitterAttackLoadout.ReplaceItems(grid.PortId, null);
            }
            _transmitterPortIds.Remove(grid);
        }

        /// <summary>
        /// 根据网格类型将物品集合同步到中心或发射器装备状态。
        /// </summary>
        /// <param name="grid">发生变化的库存网格。</param>
        private void SynchronizeLoadoutFromGrid(GridView grid)
        {
            if (grid == null)
            {
                return;
            }

            if (grid.GridType == GridType.CentralBackpack)
            {
                if (!_isConsumingSkillItem)
                {
                    _globalEffectReset?.ClearAll();
                    _centralEffectReset?.ClearPermanentEffects();
                }
                // 技能动作先于来源消费执行；消费不等于手动卸载，限时效果应继续生效。

                _centralSkillLoadout.ReplaceItems(grid.Items);
                return;
            }

            if (grid.GridType != GridType.TransmitterBackpack)
            {
                return;
            }

            _transmitterPortIds.TryGetValue(grid, out string previousPortId);
            if (!string.IsNullOrEmpty(previousPortId) && previousPortId != grid.PortId)
            {
                _transmitterAttackLoadout.ReplaceItems(previousPortId, null);
            }
            if (!string.IsNullOrEmpty(grid.PortId))
            {
                _transmitterAttackLoadout.ReplaceItems(grid.PortId, grid.Items);
                _transmitterPortIds[grid] = grid.PortId;
            }
            else
            {
                _transmitterPortIds.Remove(grid);
            }
        }

        /// <summary>在玩家完成新局初始化后重新投影所有已绑定网格。</summary>
        private void SynchronizeAllGrids()
        {
            for (int index = 0; index < _boundGrids.Count; index++)
            {
                SynchronizeLoadoutFromGrid(_boundGrids[index]);
            }
        }

        /// <summary>
        /// 在线性扫描所有已绑定网格后查找指定物品实例。
        /// </summary>
        /// <param name="itemInstanceId">要查找的物品实例标识。</param>
        /// <param name="grid">找到物品时返回所属网格，否则为 <c>null</c>。</param>
        /// <param name="item">找到物品时返回对象，否则为 <c>null</c>。</param>
        /// <returns>找到匹配物品时返回 <c>true</c>。</returns>
        /// <remarks>复杂度为已绑定网格数量乘以各网格物品数量；依赖物品实例标识全局唯一。</remarks>
        private bool FindBoundItemByInstanceId(int itemInstanceId, out GridView grid, out ItemVM item)
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
