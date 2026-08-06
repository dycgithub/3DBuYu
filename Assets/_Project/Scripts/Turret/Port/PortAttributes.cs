using System;
using System.Collections.Generic;
using Interfaces;
using InventorySystem;
using ItemSystem;
using UnityEngine;

namespace TurretSystem
{
    public class PortAttributes : IAttributesAggregator
    {
        public enum StatType
        {
            Damage, Range, FireRate, ProjectileCount, CriticalChance,
            CriticalDamage, Penetration, TrackingSpeed
        }

        private static readonly Dictionary<StatType, float> DefaultBaseValues = new()
        {
            { StatType.Damage, 10f },
            { StatType.Range, 5f },
            { StatType.FireRate, 1f },
            { StatType.ProjectileCount, 1f },
            { StatType.CriticalChance, 0.05f },
            { StatType.CriticalDamage, 1.5f },
            { StatType.Penetration, 0f },
            { StatType.TrackingSpeed, 10f },
        };

        private IReadOnlyList<PlacedItem> _lastItems;
        private InventoryGrid _lastGrid;

        private float _baseDamage;
        private float _baseRange;
        private float _baseFireRate;
        private int _baseProjectileCount;
        private float _baseCriticalChance;
        private float _baseCriticalDamage;
        private int _basePenetration;
        private float _baseTrackingSpeed;

        private float _damage;
        private float _range;
        private float _fireRate;
        private int _projectileCount;
        private float _criticalChance;
        private float _criticalDamage;
        private int _penetration;
        private float _trackingSpeed;

        public float BaseDamage { get => _baseDamage; set => _baseDamage = value; }
        public float BaseRange { get => _baseRange; set => _baseRange = value; }
        public float BaseFireRate { get => _baseFireRate; set => _baseFireRate = value; }
        public int BaseProjectileCount { get => _baseProjectileCount; set => _baseProjectileCount = value; }
        public float BaseCriticalChance { get => _baseCriticalChance; set => _baseCriticalChance = value; }
        public float BaseCriticalDamage { get => _baseCriticalDamage; set => _baseCriticalDamage = value; }
        public int BasePenetration { get => _basePenetration; set => _basePenetration = value; }
        public float BaseTrackingSpeed { get => _baseTrackingSpeed; set => _baseTrackingSpeed = value; }

        public float Damage => _damage;
        public float Range => _range;
        public float FireRate => _fireRate;
        public int ProjectileCount => _projectileCount;
        public float CriticalChance => _criticalChance;
        public float CriticalDamage => _criticalDamage;
        public int Penetration => _penetration;
        public float TrackingSpeed => _trackingSpeed;

        public bool HasHoming { get; private set; }
        public float HomingStrength { get; private set; }
        public bool HasAreaDamage { get; private set; }
        public float AreaDamageRadius { get; private set; }

        public event Action OnAttributesChanged;

        public PortAttributes()
        {
            ResetToBase();
        }

        public PortAttributes(PortAttributes template)
        {
            if (template != null)
            {
                _baseDamage = template._baseDamage;
                _baseRange = template._baseRange;
                _baseFireRate = template._baseFireRate;
                _baseProjectileCount = template._baseProjectileCount;
                _baseCriticalChance = template._baseCriticalChance;
                _baseCriticalDamage = template._baseCriticalDamage;
                _basePenetration = template._basePenetration;
                _baseTrackingSpeed = template._baseTrackingSpeed;
            }
            ResetToBase();
        }

        public void Recalculate(IReadOnlyList<PlacedItem> items, InventoryGrid grid)
        {
            ResetToBase();

            if (items == null || grid == null) return;

            foreach (var item in items)
            {
                var config = grid.GetItemConfig(item.instanceId);
                if (config == null) continue;
                ApplyItemBonuses(config);
            }

            ClampValues();

            _lastItems = items;
            _lastGrid = grid;

            OnAttributesChanged?.Invoke();
        }

        private void ApplyItemBonuses(ItemConfig config)
        {
            _damage += config.attackBonus;
            _range += config.rangeBonus;
            _criticalChance += config.criticalChanceBonus;
            _criticalDamage += config.criticalDamageBonus;

            switch (config.ItemType)
            {
                case ItemType.Skill:
                case ItemType.Ammunition:
                    if (config.fireRateModifier > 0f)
                        _fireRate *= config.fireRateModifier;
                    if (config.attackSpeedBonus > 0f)
                        _fireRate *= (1f + config.attackSpeedBonus);
                    _projectileCount += config.projectileCountModifier;
                    break;
            }
        }

        private void ClampValues()
        {
            _criticalChance = Mathf.Clamp01(_criticalChance);
            _fireRate = Mathf.Max(0.1f, _fireRate);
            _projectileCount = Mathf.Max(1, _projectileCount);
        }

        public void ResetToBase()
        {
            _damage = _baseDamage = DefaultBaseValues[StatType.Damage];
            _range = _baseRange = DefaultBaseValues[StatType.Range];
            _fireRate = _baseFireRate = DefaultBaseValues[StatType.FireRate];
            _projectileCount = _baseProjectileCount = (int)DefaultBaseValues[StatType.ProjectileCount];
            _criticalChance = _baseCriticalChance = DefaultBaseValues[StatType.CriticalChance];
            _criticalDamage = _baseCriticalDamage = DefaultBaseValues[StatType.CriticalDamage];
            _penetration = _basePenetration = (int)DefaultBaseValues[StatType.Penetration];
            _trackingSpeed = _baseTrackingSpeed = DefaultBaseValues[StatType.TrackingSpeed];
            HasHoming = false;
            HomingStrength = 0f;
            HasAreaDamage = false;
            AreaDamageRadius = 0f;
        }

        public string GetDescription()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"伤害: {Damage:F0}  射程: {Range:F1}  射速: {FireRate:F2}/s");
            sb.AppendLine($"弹丸: {ProjectileCount}  暴击: {CriticalChance:P0}×{CriticalDamage:F1}");
            sb.Append($"穿透: {Penetration}  追踪: {(HasHoming ? "是" : "否")}");
            sb.AppendLine();
            sb.Append($"跟踪: {TrackingSpeed:F1}");
            if (HasAreaDamage)
                sb.Append($"  范围伤害: {AreaDamageRadius:F1}m");
            return sb.ToString();
        }
    }
}
