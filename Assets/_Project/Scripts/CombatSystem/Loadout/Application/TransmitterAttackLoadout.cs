using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>按 PortId 缓存发射器背包内容，并按物品实例强度计算攻击修正。</summary>
    public sealed class TransmitterAttackLoadout : ITransmitterAttackModifierSource
    {
        private readonly ItemCombatDefinitionResolver _definitionResolver;
        private readonly Dictionary<string, CachedPort> _cachedPorts =
            new(StringComparer.Ordinal);

        public TransmitterAttackLoadout(ItemCombatDefinitionResolver definitionResolver)
        {
            _definitionResolver = definitionResolver;
        }

        public void BeginRun() => ClearAllItems();

        public void ReplaceItems(string portId, IEnumerable<ItemVM> items)
        {
            if (string.IsNullOrWhiteSpace(portId))
            {
                return;
            }

            List<ItemVM> itemSnapshot = CreateItemSnapshot(items);
            if (itemSnapshot.Count == 0)
            {
                if (_cachedPorts.Remove(portId, out CachedPort removedPort))
                {
                    removedPort.Dispose();
                }
                return;
            }

            if (_cachedPorts.TryGetValue(portId, out CachedPort cachedPort)
                && cachedPort.HasSameItems(itemSnapshot))
            {
                return;
            }

            if (_cachedPorts.Remove(portId, out CachedPort replacedPort))
            {
                replacedPort.Dispose();
            }

            _cachedPorts[portId] = new CachedPort(itemSnapshot);
        }

        public void CollectAttackModifiers(string portId, ref TransmitterAttackModifiers modifiers)
        {
            if (string.IsNullOrEmpty(portId)
                || !_cachedPorts.TryGetValue(portId, out CachedPort cachedPort))
            {
                return;
            }

            if (!cachedPort.HasCachedModifiers)
            {
                cachedPort.Modifiers = BuildModifiers(
                    cachedPort.Items,
                    out cachedPort.OwnedProjectileProfile);
                cachedPort.HasCachedModifiers = true;
            }

            MergeModifiers(ref modifiers, in cachedPort.Modifiers);
        }

        public void ClearAllItems()
        {
            foreach (CachedPort cachedPort in _cachedPorts.Values)
            {
                cachedPort.Dispose();
            }

            _cachedPorts.Clear();
        }

        private List<ItemVM> CreateItemSnapshot(IEnumerable<ItemVM> items)
        {
            var snapshot = new List<ItemVM>();
            if (items == null)
            {
                return snapshot;
            }

            foreach (ItemVM item in items)
            {
                if (item != null && IsTransmitterItem(item))
                {
                    snapshot.Add(item);
                }
            }

            return snapshot;
        }

        private bool IsTransmitterItem(ItemVM item)
        {
            if (!_definitionResolver.TryResolveCombatScope(item.Definition, out CombatScope scope))
            {
                return false;
            }

            return scope == CombatScope.Transmitter || scope == CombatScope.CentralAndTransmitter;
        }

        private TransmitterAttackModifiers BuildModifiers(
            IReadOnlyList<ItemVM> items,
            out ProjectileProfileSO ownedProjectileProfile)
        {
            TransmitterAttackModifiers modifiers = TransmitterAttackModifiers.Default;
            var projectileProfiles = new List<ProjectileProfileSO>();
            for (int index = 0; index < items.Count; index++)
            {
                TransmitterAttackModifiers itemModifiers = TransmitterAttackModifiers.Default;
                ApplyItemModifiers(items[index], ref itemModifiers, projectileProfiles);
                ScaleItemModifiers(ref itemModifiers, Supply.ItemPowerTable.GetScale(items[index].PowerLevel));
                MergeModifiers(ref modifiers, in itemModifiers);
            }

            ownedProjectileProfile = ProjectileProfileComposer.CreateCombinedProfile(
                projectileProfiles,
                out ProjectileProfileSO selectedProfile);
            modifiers.ProjectileProfileOverride = ownedProjectileProfile ?? selectedProfile;

            modifiers.ClampToValidRanges();
            return modifiers;
        }

        private void ApplyItemModifiers(
            ItemVM item,
            ref TransmitterAttackModifiers modifiers,
            List<ProjectileProfileSO> projectileProfiles)
        {
            if (!_definitionResolver.TryResolveCombatScope(item.Definition, out CombatScope scope)
                || (scope != CombatScope.Transmitter && scope != CombatScope.CentralAndTransmitter))
            {
                return;
            }

            if (item.Definition.CombatDefinition == null)
            {
                return;
            }

            ItemCombatDefinitionSO combat = _definitionResolver.ResolveCombatDefinition(item.Definition);

            modifiers.AdditiveDamageBonus += combat.TransmitterAdditiveDamageBonus;
            modifiers.EnergyCostMultiplier *= combat.TransmitterEnergyCostMultiplier;
            if (combat.TransmitterProjectileProfile != null
                && !projectileProfiles.Contains(combat.TransmitterProjectileProfile))
            {
                projectileProfiles.Add(combat.TransmitterProjectileProfile);
            }

            TransmitterAttackModifierDefinitionSO[] definitions = combat.TransmitterAttackModifierDefinitions;
            if (definitions == null)
            {
                return;
            }

            for (int index = 0; index < definitions.Length; index++)
            {
                definitions[index]?.Apply(ref modifiers);
            }
        }

        /// <summary>按物品强度缩放连续战斗数值，保留离散配置和计数语义。</summary>
        private static void ScaleItemModifiers(
            ref TransmitterAttackModifiers modifiers,
            float effectScale)
        {
            modifiers.AdditiveDamageBonus *= effectScale;
            modifiers.DamageMultiplier = ScaleMultiplier(modifiers.DamageMultiplier, effectScale);
            modifiers.EnergyCostMultiplier = ScaleMultiplier(
                modifiers.EnergyCostMultiplier,
                effectScale);
            modifiers.RangeMultiplier = ScaleMultiplier(modifiers.RangeMultiplier, effectScale);
            modifiers.AttackSpeedMultiplier = ScaleMultiplier(modifiers.AttackSpeedMultiplier, effectScale);
            modifiers.CollisionRadiusMultiplier = ScaleMultiplier(
                modifiers.CollisionRadiusMultiplier,
                effectScale);
            modifiers.AdditionalProjectileCount = ScaleCount(
                modifiers.AdditionalProjectileCount,
                effectScale);
            modifiers.AdditionalPenetrationCount = ScaleCount(
                modifiers.AdditionalPenetrationCount,
                effectScale);
            modifiers.AdditionalCriticalChance *= effectScale;
            modifiers.CriticalDamageMultiplier = ScaleMultiplier(
                modifiers.CriticalDamageMultiplier,
                effectScale);
        }

        /// <summary>按实例强度缩放整数属性并使用四舍五入，结果不低于零。</summary>
        /// <param name="value">物品提供的整数贡献。</param>
        /// <param name="effectScale">统一强度倍率。</param>
        /// <returns>缩放后的非负整数贡献。</returns>
        private static int ScaleCount(int value, float effectScale)
        {
            return Mathf.Max(0, Mathf.RoundToInt(value * effectScale));
        }

        /// <summary>围绕中性倍率 1 缩放一个物品贡献，确保零强度保持原值。</summary>
        private static float ScaleMultiplier(float multiplier, float effectScale)
        {
            return 1f + (multiplier - 1f) * effectScale;
        }

        /// <summary>将单个物品贡献合并到端口总修正，并保留最后的离散类型覆盖。</summary>
        private static void MergeModifiers(
            ref TransmitterAttackModifiers modifiers,
            in TransmitterAttackModifiers contribution)
        {
            modifiers.AdditiveDamageBonus += contribution.AdditiveDamageBonus;
            modifiers.DamageMultiplier *= contribution.DamageMultiplier;
            modifiers.EnergyCostMultiplier *= contribution.EnergyCostMultiplier;
            modifiers.RangeMultiplier *= contribution.RangeMultiplier;
            modifiers.AttackSpeedMultiplier *= contribution.AttackSpeedMultiplier;
            modifiers.CollisionRadiusMultiplier *= contribution.CollisionRadiusMultiplier;
            modifiers.AdditionalProjectileCount += contribution.AdditionalProjectileCount;
            modifiers.AdditionalPenetrationCount += contribution.AdditionalPenetrationCount;
            modifiers.AdditionalCriticalChance += contribution.AdditionalCriticalChance;
            modifiers.CriticalDamageMultiplier *= contribution.CriticalDamageMultiplier;
            if (contribution.DamageTypeOverride.HasValue)
            {
                modifiers.DamageTypeOverride = contribution.DamageTypeOverride;
            }

            if (modifiers.ProjectileProfileOverride == null)
            {
                modifiers.ProjectileProfileOverride = contribution.ProjectileProfileOverride;
            }
        }

        private sealed class CachedPort
        {
            public readonly List<ItemVM> Items;
            public TransmitterAttackModifiers Modifiers;
            public ProjectileProfileSO OwnedProjectileProfile;
            public bool HasCachedModifiers;

            public CachedPort(List<ItemVM> items)
            {
                Items = items;
            }

            public bool HasSameItems(IReadOnlyList<ItemVM> items)
            {
                if (Items.Count != items.Count)
                {
                    return false;
                }

                for (int index = 0; index < Items.Count; index++)
                {
                    ItemVM cachedItem = Items[index];
                    ItemVM currentItem = items[index];
                    if (cachedItem.InstanceId != currentItem.InstanceId
                        || cachedItem.PowerLevel != currentItem.PowerLevel
                        || !ReferenceEquals(cachedItem.Definition, currentItem.Definition))
                    {
                        return false;
                    }
                }

                return true;
            }

            public void Dispose()
            {
                if (OwnedProjectileProfile == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(OwnedProjectileProfile);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(OwnedProjectileProfile);
                }

                OwnedProjectileProfile = null;
            }
        }
    }
}
