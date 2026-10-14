using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>合并道具投射物机制，并按装备顺序选择专属外观和命中音效。</summary>
    internal static class ProjectileProfileComposer
    {
        /// <summary>组合装备配置、命中效果、专属外观和命中音效。</summary>
        /// <param name="profiles">装备提供的投射物配置。</param>
        /// <param name="selectedProfile">被选作基础数值和轨迹来源的配置。</param>
        /// <returns>装备配置的独立合并副本；没有有效配置时返回 null。</returns>
        internal static ProjectileProfileSO CreateCombinedProfile(
            IReadOnlyList<ProjectileProfileSO> profiles,
            out ProjectileProfileSO selectedProfile)
        {
            selectedProfile = SelectTrajectoryProfile(profiles);
            if (selectedProfile == null)
            {
                return null;
            }

            ProjectileProfileSO combined = ProjectileProfileSO.CreateRuntimeProfile(
                "CombinedTransmitterProjectileProfile");
            CopyProfileValues(selectedProfile, combined);
            combined.TrajectoryDefinition = selectedProfile.TrajectoryDefinition;
            combined.VisualDefinition = SelectVisualDefinition(profiles);
            combined.ImpactSound = SelectImpactSound(profiles, selectedProfile.ImpactSound);
            MergeProfileMechanics(profiles, combined);
            combined.OnHitEffectDefinitions = MergeEffects(profiles, ProfileEffectKind.OnHit);
            combined.OnExpiredEffectDefinitions = MergeEffects(profiles, ProfileEffectKind.OnExpired);
            combined.BeforeHitEffectDefinitions = MergeEffects(profiles, ProfileEffectKind.BeforeHit);
            return combined;
        }

        /// <summary>选择装备顺序中最后一个非空命中音效，无装备音效时保留基础配置值。</summary>
        /// <param name="profiles">按成功放置顺序排列的道具投射物配置。</param>
        /// <param name="baseImpactSound">被选作基础配置的命中音效。</param>
        /// <returns>最后一个非空装备音效，或基础配置的命中音效。</returns>
        private static AudioClip SelectImpactSound(
            IReadOnlyList<ProjectileProfileSO> profiles,
            AudioClip baseImpactSound)
        {
            AudioClip selectedSound = baseImpactSound;
            if (profiles == null)
            {
                return selectedSound;
            }

            for (int index = 0; index < profiles.Count; index++)
            {
                AudioClip impactSound = profiles[index]?.ImpactSound;
                if (impactSound != null)
                {
                    selectedSound = impactSound;
                }
            }

            return selectedSound;
        }

        /// <summary>按装备顺序选择最近一个带专属外观的弹体视觉。</summary>
        /// <param name="profiles">按成功放置顺序排列的道具投射物配置。</param>
        /// <returns>最近的专属外观；不存在时返回空值。</returns>
        private static ProjectileVisualDefinitionSO SelectVisualDefinition(
            IReadOnlyList<ProjectileProfileSO> profiles)
        {
            ProjectileVisualDefinitionSO selectedVisual = null;
            if (profiles == null)
            {
                return null;
            }

            for (int index = 0; index < profiles.Count; index++)
            {
                ProjectileVisualDefinitionSO visual = profiles[index]?.VisualDefinition;
                if (visual != null && visual.OverridesEmitterVisual)
                {
                    selectedVisual = visual;
                }
            }

            return selectedVisual;
        }

        /// <summary>复制基础配置的数值与表现字段。</summary>
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
            destination.ScatterAngleDegrees = source.ScatterAngleDegrees;
            destination.RandomSpeedVariance = source.RandomSpeedVariance;
            destination.EmitProjectilesSequentially = source.EmitProjectilesSequentially;
            destination.SequentialProjectileCount = source.SequentialProjectileCount;
            destination.RandomDirectionAngleDegrees = source.RandomDirectionAngleDegrees;
            destination.ProjectileSpeedMultiplier = source.ProjectileSpeedMultiplier;
            destination.ExecutionHealthThreshold = source.ExecutionHealthThreshold;
            destination.ExecutionDamageMultiplier = source.ExecutionDamageMultiplier;
            destination.ComboDamageBonusPerHit = source.ComboDamageBonusPerHit;
            destination.ComboMaximumStacks = source.ComboMaximumStacks;
            destination.ComboResetSeconds = source.ComboResetSeconds;
            destination.KillEnergyRefundRatio = source.KillEnergyRefundRatio;
            destination.RicochetCount = source.RicochetCount;
            destination.RicochetSearchRadius = source.RicochetSearchRadius;
            destination.RicochetDamageMultiplier = source.RicochetDamageMultiplier;
            destination.SplitProjectileCount = source.SplitProjectileCount;
            destination.SplitAngleDegrees = source.SplitAngleDegrees;
            destination.SplitTargetSearchRadius = source.SplitTargetSearchRadius;
            destination.DamageType = source.DamageType;
            destination.VisualDefinition = source.VisualDefinition;
            destination.HitEffectId = source.HitEffectId;
            destination.ExpiredEffectId = source.ExpiredEffectId;
        }

        /// <summary>合并多个配置中可共存的投射物机制。</summary>
        private static void MergeProfileMechanics(
            IReadOnlyList<ProjectileProfileSO> profiles,
            ProjectileProfileSO destination)
        {
            if (profiles == null || destination == null)
            {
                return;
            }

            destination.ScatterAngleDegrees = 0f;
            destination.RandomSpeedVariance = 0f;
            destination.EmitProjectilesSequentially = false;
            destination.SequentialProjectileCount = 0;
            destination.RandomDirectionAngleDegrees = 0f;
            destination.ProjectileSpeedMultiplier = 1f;
            destination.ExecutionHealthThreshold = 0f;
            destination.ExecutionDamageMultiplier = 1f;
            destination.ComboDamageBonusPerHit = 0f;
            destination.ComboMaximumStacks = 0;
            destination.ComboResetSeconds = 0f;
            destination.KillEnergyRefundRatio = 0f;
            destination.RicochetCount = 0;
            destination.RicochetSearchRadius = 0f;
            destination.RicochetDamageMultiplier = 1f;
            destination.SplitProjectileCount = 0;
            destination.SplitAngleDegrees = 0f;
            destination.SplitTargetSearchRadius = 0f;

            float refundRatio = 0f;
            for (int index = 0; index < profiles.Count; index++)
            {
                ProjectileProfileSO profile = profiles[index];
                if (profile == null)
                {
                    continue;
                }

                destination.ScatterAngleDegrees = Mathf.Max(
                    destination.ScatterAngleDegrees,
                    profile.ScatterAngleDegrees);
                destination.RandomSpeedVariance = Mathf.Max(
                    destination.RandomSpeedVariance,
                    profile.RandomSpeedVariance);
                destination.EmitProjectilesSequentially |= profile.EmitProjectilesSequentially;
                destination.SequentialProjectileCount = Mathf.Max(
                    destination.SequentialProjectileCount,
                    profile.SequentialProjectileCount);
                destination.RandomDirectionAngleDegrees = Mathf.Max(
                    destination.RandomDirectionAngleDegrees,
                    profile.RandomDirectionAngleDegrees);
                destination.ProjectileSpeedMultiplier *= Mathf.Max(
                    0f,
                    profile.ProjectileSpeedMultiplier);
                destination.ExecutionHealthThreshold = Mathf.Max(
                    destination.ExecutionHealthThreshold,
                    profile.ExecutionHealthThreshold);
                destination.ExecutionDamageMultiplier = Mathf.Max(
                    destination.ExecutionDamageMultiplier,
                    profile.ExecutionDamageMultiplier);
                destination.ComboDamageBonusPerHit += profile.ComboDamageBonusPerHit;
                destination.ComboMaximumStacks = Mathf.Max(
                    destination.ComboMaximumStacks,
                    profile.ComboMaximumStacks);
                destination.ComboResetSeconds = Mathf.Max(
                    destination.ComboResetSeconds,
                    profile.ComboResetSeconds);
                refundRatio += profile.KillEnergyRefundRatio;
                destination.RicochetCount += profile.RicochetCount;
                destination.RicochetSearchRadius = Mathf.Max(
                    destination.RicochetSearchRadius,
                    profile.RicochetSearchRadius);
                destination.RicochetDamageMultiplier = Mathf.Min(
                    destination.RicochetDamageMultiplier,
                    profile.RicochetDamageMultiplier);
                destination.SplitProjectileCount += profile.SplitProjectileCount;
                destination.SplitAngleDegrees = Mathf.Max(
                    destination.SplitAngleDegrees,
                    profile.SplitAngleDegrees);
                destination.SplitTargetSearchRadius = Mathf.Max(
                    destination.SplitTargetSearchRadius,
                    profile.SplitTargetSearchRadius);
            }

            destination.KillEnergyRefundRatio = Mathf.Clamp01(refundRatio);
        }

        /// <summary>按配置顺序合并效果并删除重复的资产引用。</summary>
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

        /// <summary>从配置中选出优先级最高的投射物轨迹。</summary>
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

        /// <summary>定义不能同时执行的轨迹配置的选择顺序。</summary>
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

        private enum ProfileEffectKind
        {
            OnHit,
            OnExpired,
            BeforeHit
        }
    }
}
