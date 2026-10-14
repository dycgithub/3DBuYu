namespace CombatSystem
{
    /// <summary>
    /// 将端口输入和装备修正合并为最终攻击参数，并补齐缺失的默认表现配置。
    /// </summary>
    public sealed class TransmitterAttackBuildService
    {
        private readonly ITransmitterAttackModifierSource _modifierSource;
        private readonly ITransmitterGlobalEffectService _globalEffects;

        /// <summary>创建发射器攻击参数构建服务。</summary>
        /// <param name="modifierSource">提供端口装备攻击修正的来源。</param>
        /// <param name="globalEffects">提供中央主动技能产生的全局效果。</param>
        public TransmitterAttackBuildService(
            ITransmitterAttackModifierSource modifierSource,
            ITransmitterGlobalEffectService globalEffects = null)
        {
            _modifierSource = modifierSource;
            _globalEffects = globalEffects;
        }

        /// <summary>构建指定端口输入对应的攻击参数。</summary>
        /// <param name="input">包含发射器基础值和子弹配置的端口输入。</param>
        /// <returns>包含所有装备修正的攻击参数；子弹配置为空时保留空引用并由攻击构建器拒绝执行。</returns>
        public TransmitterAttackBuild BuildAttackParameters(in TransmitterAttackInput input)
        {
            TransmitterAttackModifiers modifiers = TransmitterAttackModifiers.Default;
            _modifierSource?.CollectAttackModifiers(input.PortId, ref modifiers);
            modifiers.ClampToValidRanges();

            ProjectileProfileSO profile = modifiers.ProjectileProfileOverride ?? input.ProjectileProfile;
            if (profile != null && !ReferenceEquals(profile, input.ProjectileProfile))
            {
                if (profile.VisualDefinition == null)
                {
                    profile.VisualDefinition = input.ProjectileProfile?.VisualDefinition;
                }

                if (profile.ImpactSound == null)
                {
                    profile.ImpactSound = input.ProjectileProfile?.ImpactSound;
                }
            }

            float globalAttackSpeedMultiplier = _globalEffects != null
                ? UnityEngine.Mathf.Max(0f, _globalEffects.AttackSpeedMultiplier)
                : 1f;
            float attacksPerSecond = UnityEngine.Mathf.Max(0.001f, input.AttacksPerSecond)
                * modifiers.AttackSpeedMultiplier
                * globalAttackSpeedMultiplier;
            float collisionRadius = profile != null
                ? UnityEngine.Mathf.Max(0f, profile.CollisionRadius)
                    * modifiers.CollisionRadiusMultiplier
                : 0f;
            DamageType damageType = modifiers.DamageTypeOverride
                ?? (profile != null ? profile.DamageType : DamageType.Physical);

            return new TransmitterAttackBuild(
                profile,
                modifiers.AdditiveDamageBonus,
                modifiers.DamageMultiplier,
                modifiers.EnergyCostMultiplier,
                modifiers.RangeMultiplier,
                attacksPerSecond,
                collisionRadius,
                damageType,
                UnityEngine.Mathf.Max(
                    1,
                    input.RequestedProjectileCount + modifiers.AdditionalProjectileCount),
                UnityEngine.Mathf.Max(
                    0,
                    input.RequestedPenetrationCount + modifiers.AdditionalPenetrationCount),
                UnityEngine.Mathf.Clamp01(input.CriticalHitChance + modifiers.AdditionalCriticalChance),
                UnityEngine.Mathf.Max(
                    1f,
                    input.CriticalHitDamageMultiplier * modifiers.CriticalDamageMultiplier));
        }
    }
}
