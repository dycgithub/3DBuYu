using System;
using System.Collections.Generic;
using Interfaces;
using UnityEngine;
using VContainer;
using ItemSystem;

namespace InventorySystem
{
    /// <summary>
    /// 耐久度
    /// </summary>
    public class DurabilityManager : MonoBehaviour
    {
        public event Action<int> OnItemBroken;
        public event Action<int> OnItemExpired;
        public event Action<int> OnItemRepaired;
        public event Action<int, int, int> OnDurabilityChanged;
        public event Action<int, float, float> OnTimeChanged;

        private readonly Dictionary<int, DurabilityState> _trackedItems
            = new Dictionary<int, DurabilityState>();

        private readonly Dictionary<int, IInventory> _itemToInventory
            = new Dictionary<int, IInventory>();

        private readonly List<(IInventory inv, Action<PlacedItem> placed, Action<int> removed)>
            _registeredInventories = new();

        public IReadOnlyDictionary<int, DurabilityState> TrackedItems => _trackedItems;

        [Inject] private Services.IPointsService _pointsService;

        private bool _isPaused = true;

        public void SetPaused(bool paused) => _isPaused = paused;

        private void Awake()
        {
            OnItemBroken += HandleItemBroken;
            OnItemExpired += HandleItemExpired;
        }

        private void OnDestroy()
        {
            OnItemBroken -= HandleItemBroken;
            OnItemExpired -= HandleItemExpired;

            // 清理对全局库存(PlayerLoadout)的悬挂委托,防止场景卸载后事件引用已销毁的 DurabilityManager
            foreach (var entry in _registeredInventories)
            {
                if (entry.inv == null) continue;
                entry.inv.OnItemPlaced -= entry.placed;
                entry.inv.OnItemRemoved -= entry.removed;
            }
            _registeredInventories.Clear();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void RegisterInventory(IInventory inventory)
        {
            if (inventory == null) return;
            foreach (var entry in _registeredInventories)
                if (ReferenceEquals(entry.inv, inventory)) return;

            Action<PlacedItem> placedHandler = placed =>
            {
                var config = inventory.GetItemConfig(placed.instanceId);
                if (config != null)
                {
                    StartTracking(placed.instanceId, config);
                    _itemToInventory[placed.instanceId] = inventory;
                }
            };
            Action<int> removedHandler = instanceId =>
            {
                StopTracking(instanceId);
                _itemToInventory.Remove(instanceId);
            };

            inventory.OnItemPlaced += placedHandler;
            inventory.OnItemRemoved += removedHandler;
            _registeredInventories.Add((inventory, placedHandler, removedHandler));

            foreach (var placed in inventory.GetAllItems())
            {
                var config = inventory.GetItemConfig(placed.instanceId);
                if (config != null)
                {
                    StartTracking(placed.instanceId, config);
                    _itemToInventory[placed.instanceId] = inventory;
                }
            }
        }

        public void UnregisterInventory(IInventory inventory)
        {
            if (inventory == null) return;
            for (int i = _registeredInventories.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_registeredInventories[i].inv, inventory))
                {
                    inventory.OnItemPlaced -= _registeredInventories[i].placed;
                    inventory.OnItemRemoved -= _registeredInventories[i].removed;
                    _registeredInventories.RemoveAt(i);
                }
            }
        }

        private void HandleItemBroken(int instanceId)
        {
            if (_itemToInventory.TryGetValue(instanceId, out var inventory))
            {
                _itemToInventory.Remove(instanceId);
                inventory.RemoveItem(instanceId);
                Debug.Log($"[DurabilityManager] 道具破碎，已从库存移除: {instanceId}");
            }
        }

        private void HandleItemExpired(int instanceId)
        {
            if (_itemToInventory.TryGetValue(instanceId, out var inventory))
            {
                _itemToInventory.Remove(instanceId);
                inventory.RemoveItem(instanceId);
                Debug.Log($"[DurabilityManager] 道具过期，已从库存移除: {instanceId}");
            }
        }

        public void StartTracking(int instanceId, ItemConfig config)
        {
            if (config == null || instanceId < 0) return;

            if (_trackedItems.ContainsKey(instanceId))
                return;

            var state = new DurabilityState
            {
                instanceId = instanceId,
                maxDurability = config.maxDurability,
                currentDurability = config.maxDurability,
                maxUsageTime = config.maxUsageTime,
                remainingTime = config.maxUsageTime,
                isBroken = false,
                isExpired = false
            };

            _trackedItems[instanceId] = state;
        }

        public void StopTracking(int instanceId)
        {
            _trackedItems.Remove(instanceId);
        }

        /// <summary>
        /// 消耗物品耐久。成功消耗返回 true；物品已破碎返回 false。
        /// 未追踪物品视为未启用耐久机制（如无耐久配置的弹药），永久可用。
        /// </summary>
        public bool ConsumeDurability(int instanceId, int amount = 1)
        {
            if (!_trackedItems.TryGetValue(instanceId, out var state))
                return true;

            if (!state.HasDurability)
                return true;

            if (state.isBroken)
                return false;

            int oldValue = state.currentDurability;
            state.currentDurability = Mathf.Max(0, state.currentDurability - amount);
            _trackedItems[instanceId] = state;

            OnDurabilityChanged?.Invoke(instanceId, oldValue, state.currentDurability);

            if (state.currentDurability <= 0)
            {
                state.isBroken = true;
                _trackedItems[instanceId] = state;
                OnItemBroken?.Invoke(instanceId);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取物品耐久状态。未追踪的物品返回 null（调用方按无耐久处理）。
        /// </summary>
        public DurabilityState? GetDurability(int instanceId)
        {
            return _trackedItems.TryGetValue(instanceId, out var state) ? state : (DurabilityState?)null;
        }

        /// <summary>修理已破碎物品:先扣积分(积分不足则失败),再恢复耐久。</summary>
        public bool Repair(int instanceId, ItemConfig config)
        {
            if (config == null) return false;

            var shop = ProjectLifetimeScope.Instance?.Container?.Resolve<InventorySystem.Shop.ShopConfig>();
            int repairCost = shop != null ? shop.GetRepairCost(config.itemId) : 0;
            if (repairCost <= 0) return false;

            if (!_trackedItems.TryGetValue(instanceId, out var state))
                return false;

            if (!state.isBroken)
                return false;

            if (_pointsService == null)
                return false;

            if (!_pointsService.SpendPoints(repairCost, $"修理 {config.displayName}"))
            {
                Debug.Log($"[DurabilityManager] 修理失败: 积分不足 ({repairCost})");
                return false;
            }

            state.currentDurability = state.maxDurability;
            state.isBroken = false;
            _trackedItems[instanceId] = state;

            OnItemRepaired?.Invoke(instanceId);
            OnDurabilityChanged?.Invoke(instanceId, 0, state.currentDurability);

            Debug.Log($"[DurabilityManager] 修理成功: {config.displayName}, 花费 {repairCost} 积分");
            return true;
        }

        public void DestroyItem(int instanceId)
        {
            _trackedItems.Remove(instanceId);
        }

        private void Tick(float deltaTime)
        {
            if (_isPaused)
                return;

            var expiredIds = new List<int>();

            foreach (var kvp in _trackedItems)
            {
                var state = kvp.Value;
                if (!state.HasUsageTime || state.isExpired)
                    continue;

                state.remainingTime -= deltaTime;
                _trackedItems[kvp.Key] = state;

                OnTimeChanged?.Invoke(kvp.Key, state.remainingTime, state.maxUsageTime);

                if (state.remainingTime <= 0f)
                {
                    state.remainingTime = 0f;
                    state.isExpired = true;
                    _trackedItems[kvp.Key] = state;
                    expiredIds.Add(kvp.Key);
                }
            }

            foreach (int id in expiredIds)
                OnItemExpired?.Invoke(id);
        }
    }
}