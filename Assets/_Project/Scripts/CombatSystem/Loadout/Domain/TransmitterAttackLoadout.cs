using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>按 PortId 缓存发射器背包内容及其惰性计算的攻击修正。</summary>
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

            TransmitterAttackModifiers cachedModifiers = cachedPort.Modifiers;
            modifiers.AdditiveDamageBonus += cachedModifiers.AdditiveDamageBonus;
            modifiers.DamageMultiplier *= cachedModifiers.DamageMultiplier;
            modifiers.RangeMultiplier *= cachedModifiers.RangeMultiplier;
            modifiers.AttackSpeedMultiplier *= cachedModifiers.AttackSpeedMultiplier;
            modifiers.CollisionRadiusMultiplier *= cachedModifiers.CollisionRadiusMultiplier;
            modifiers.AdditionalProjectileCount += cachedModifiers.AdditionalProjectileCount;
            modifiers.AdditionalPenetrationCount += cachedModifiers.AdditionalPenetrationCount;
            modifiers.AdditionalCriticalChance += cachedModifiers.AdditionalCriticalChance;
            modifiers.CriticalDamageMultiplier *= cachedModifiers.CriticalDamageMultiplier;
            if (cachedModifiers.DamageTypeOverride.HasValue)
            {
                modifiers.DamageTypeOverride = cachedModifiers.DamageTypeOverride;
            }

            if (modifiers.ProjectileProfileOverride == null)
            {
                modifiers.ProjectileProfileOverride = cachedModifiers.ProjectileProfileOverride;
            }
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
                ApplyItemModifiers(items[index], ref modifiers, projectileProfiles);
            }

            ownedProjectileProfile = CreateCombinedProjectileProfile(
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

        /// <summary>组合多个物品的命中效果，并选择唯一可执行的轨迹策略。</summary>
        private static ProjectileProfileSO CreateCombinedProjectileProfile(
            IReadOnlyList<ProjectileProfileSO> profiles,
            out ProjectileProfileSO selectedProfile)
        {
            selectedProfile = SelectTrajectoryProfile(profiles);
            if (profiles == null || profiles.Count <= 1)
            {
                return null;
            }

            ProjectileProfileSO combined = ProjectileProfileSO.CreateRuntimeProfile(
                "CombinedTransmitterProjectileProfile");
            CopyProfileValues(selectedProfile ?? profiles[0], combined);
            combined.TrajectoryDefinition = selectedProfile?.TrajectoryDefinition;
            combined.OnHitEffectDefinitions = MergeEffects(profiles, ProfileEffectKind.OnHit);
            combined.OnExpiredEffectDefinitions = MergeEffects(profiles, ProfileEffectKind.OnExpired);
            combined.BeforeHitEffectDefinitions = MergeEffects(profiles, ProfileEffectKind.BeforeHit);
            return combined;
        }

        /// <summary>复制组合 Profile 的基础数值和表现配置。</summary>
        private static void CopyProfileValues(
            ProjectileProfileSO source,
            ProjectileProfileSO destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.EnergyCost = source.EnergyCost;
            destination.SpeedUnitsPerSecond = source.SpeedUnitsPerSecond;
            destination.MaximumTravelDistance = source.MaximumTravelDistance;
            destination.CollisionRadius = source.CollisionRadius;
            destination.DamageType = source.DamageType;
            destination.VisualDefinition = source.VisualDefinition;
            destination.HitEffectId = source.HitEffectId;
            destination.ExpiredEffectId = source.ExpiredEffectId;
        }

        /// <summary>按轨迹优先级选择组合 Profile 的唯一轨迹。</summary>
        private static ProjectileProfileSO SelectTrajectoryProfile(
            IReadOnlyList<ProjectileProfileSO> profiles)
        {
            ProjectileProfileSO selected = null;
            int selectedPriority = -1;
            if (profiles == null)
            {
                return null;
            }

            for (int index = 0; index < profiles.Count; index++)
            {
                ProjectileProfileSO profile = profiles[index];
                if (profile == null)
                {
                    continue;
                }

                int priority = GetTrajectoryPriority(profile.TrajectoryDefinition);
                if (selected == null || priority > selectedPriority)
                {
                    selected = profile;
                    selectedPriority = priority;
                }
            }

            return selected;
        }

        /// <summary>定义不能同时执行的轨迹冲突解决顺序。</summary>
        private static int GetTrajectoryPriority(TrajectoryDefinitionSO trajectory)
        {
            return trajectory switch
            {
                BoomerangTrajectoryDefinitionSO => 5,
                HomingTrajectoryDefinitionSO => 4,
                LaserTrajectoryDefinitionSO => 3,
                HitscanTrajectoryDefinitionSO => 2,
                ProjectileTrajectoryDefinitionSO => 1,
                null => 0,
                _ => 1
            };
        }

        /// <summary>按原始 Profile 顺序合并效果，并删除重复的资产引用。</summary>
        private static List<ProjectileEffectDefinitionSO> MergeEffects(
            IReadOnlyList<ProjectileProfileSO> profiles,
            ProfileEffectKind effectKind)
        {
            var merged = new List<ProjectileEffectDefinitionSO>();
            if (profiles == null)
            {
                return merged;
            }

            for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
            {
                ProjectileProfileSO profile = profiles[profileIndex];
                if (profile == null)
                {
                    continue;
                }

                List<ProjectileEffectDefinitionSO> effects = effectKind switch
                {
                    ProfileEffectKind.OnHit => profile.OnHitEffectDefinitions,
                    ProfileEffectKind.OnExpired => profile.OnExpiredEffectDefinitions,
                    _ => profile.BeforeHitEffectDefinitions
                };
                if (effects == null)
                {
                    continue;
                }

                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    ProjectileEffectDefinitionSO effect = effects[effectIndex];
                    if (effect != null && !merged.Contains(effect))
                    {
                        merged.Add(effect);
                    }
                }
            }

            return merged;
        }

        private enum ProfileEffectKind
        {
            OnHit,
            OnExpired,
            BeforeHit
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
