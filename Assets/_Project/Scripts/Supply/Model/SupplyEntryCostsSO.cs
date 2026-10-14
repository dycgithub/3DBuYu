using System;
using UnityEngine;

namespace Supply
{
    /// <summary>保存三个抽奖入口各自的一次性入场费用。</summary>
    [CreateAssetMenu(fileName = "SupplyEntryCostsSO", menuName = "Supply/Entry Costs")]
    public sealed class SupplyEntryCostsSO : ScriptableObject
    {
        internal const int DefaultEntryCost = 10000;

        [SerializeField, Min(0)] private int _chestEntryCost = DefaultEntryCost;
        [SerializeField, Min(0)] private int _slotMachineEntryCost = DefaultEntryCost;
        [SerializeField, Min(0)] private int _mobiusEntryCost = DefaultEntryCost;

        /// <summary>读取指定入口的配置费用。</summary>
        /// <param name="method">抽奖入口。</param>
        /// <returns>非负入场费用。</returns>
        public int GetCost(SupplyMethod method)
        {
            int cost = method switch
            {
                SupplyMethod.Chest => _chestEntryCost,
                SupplyMethod.SlotMachine => _slotMachineEntryCost,
                SupplyMethod.Mobius => _mobiusEntryCost,
                _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
            };

            return Mathf.Max(0, cost);
        }
    }
}
