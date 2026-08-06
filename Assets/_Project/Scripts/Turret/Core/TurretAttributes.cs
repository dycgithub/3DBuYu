using System;
using System.Collections.Generic;
using Interfaces;
using InventorySystem;
using ItemSystem;
using UnityEngine;

namespace TurretSystem
{
    public class TurretAttributes : IAttributesAggregator
    {
        public float BaseDetectionRadius { get; set; } = 10f;
        public float BaseRotationSpeed { get; set; } = 180f;

        public float DetectionRadius { get; private set; }
        public float RotationSpeed { get; private set; }

        public event Action OnAttributesChanged;

        public TurretAttributes()
        {
            ResetToBase();
        }

        public TurretAttributes(float baseDetectionRadius, float baseRotationSpeed)
        {
            BaseDetectionRadius = baseDetectionRadius;
            BaseRotationSpeed = baseRotationSpeed;
            ResetToBase();
        }

        public void Recalculate(IReadOnlyList<PlacedItem> items, InventoryGrid grid)
        {
            ResetToBase();

            if (items == null || grid == null) return;

            float dr = DetectionRadius;
            float rs = RotationSpeed;

            foreach (var item in items)
            {
                var config = grid.GetItemConfig(item.instanceId);
                if (config == null) continue;

                dr += config.attackBonus;
                rs *= config.attackSpeedBonus > 0f ? (1f + config.attackSpeedBonus) : 1f;
            }

            DetectionRadius = Mathf.Max(1f, dr);
            RotationSpeed = Mathf.Max(10f, rs);

            OnAttributesChanged?.Invoke();
        }
        
        public void ResetToBase()
        {
            DetectionRadius = BaseDetectionRadius;
            RotationSpeed = BaseRotationSpeed;
        }

        public string GetDescription()
        {
            return $"探测半径: {DetectionRadius:F1}  旋转速度: {RotationSpeed:F0}";
        }
    }
}
