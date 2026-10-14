using CombatSystem;
using EffectSystem;
using FlockingSystem.ECS;
using GameSystem;
using UnityEngine;
using Utils;
using VContainer;
using VContainer.Unity;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>
    /// CombatPlayground 的独立作用域，不依赖正式场景作用域或 CentralCore。
    /// </summary>
    public sealed class CombatPlaygroundLifetimeScope : LifetimeScope
    {
        [SerializeField] private CombatPlaygroundComposition composition;
        [SerializeField] private CombatEffectCatalogSO combatEffectCatalog;
        [SerializeField] private EnemyFlockSettingsSO enemyFlockSettings;

        /// <summary>注册实验场景技能、伤害、Buff、敌人表现和投射物所需服务。</summary>
        protected override void Configure(IContainerBuilder builder)
        {
            if (composition == null)
            {
                throw new System.InvalidOperationException(
                    "CombatPlaygroundLifetimeScope 未配置 Composition。");
            }

            if (enemyFlockSettings == null)
            {
                throw new System.InvalidOperationException(
                    "CombatPlaygroundLifetimeScope 未配置 EnemyFlockSettings。");
            }

            composition.Register(builder);

            builder.Register<DamageRequestProcessor>(Lifetime.Singleton)
                .As<IDamageRequestProcessor>()
                .As<IDamageEventSource>()
                .AsSelf();
            builder.Register<AttackInstanceIdGenerator>(Lifetime.Singleton)
                .As<IAttackInstanceIdGenerator>()
                .AsSelf();
            builder.RegisterEntryPoint<BuffService>(Lifetime.Singleton)
                .As<IBuffService>()
                .AsSelf();
            builder.Register<CombatPlaygroundPhaseService>(Lifetime.Singleton)
                .As<Services.ICombatPhaseService>()
                .AsSelf();
            builder.Register<GameObjectPoolService>(Lifetime.Singleton)
                .As<IGameObjectPool>()
                .AsSelf();
            builder.Register<EnergyService>(Lifetime.Singleton)
                .As<Services.IEnergyService>()
                .AsSelf();
            builder.Register<SkillHand>(Lifetime.Singleton).AsSelf();
            builder.Register<CombatPlaygroundInventory>(Lifetime.Singleton)
                .As<ICombatItemConsumptionService>()
                .AsSelf();
            builder.Register<CombatPlaygroundAttackModifiers>(Lifetime.Singleton)
                .As<ITransmitterAttackModifierSource>()
                .AsSelf();
            builder.Register<TransmitterGlobalEffectService>(Lifetime.Singleton)
                .As<ITransmitterGlobalEffectService>()
                .As<ITransmitterGlobalEffectResetService>()
                .AsSelf();
            builder.RegisterInstance(enemyFlockSettings).AsSelf();
            builder.RegisterEntryPoint<EnemyFlockRuntimeService>(Lifetime.Singleton).AsSelf();

            new GameLoopProjectileInstaller().Install(builder);
            builder.Register<UnityAttackClock>(Lifetime.Singleton).As<IAttackClock>();
            builder.Register<AttackCooldownRegistry>(Lifetime.Singleton).AsSelf();
            builder.Register<AttackDataBuilder>(Lifetime.Singleton).AsSelf();
            builder.Register<AttackExecutor>(Lifetime.Singleton).AsSelf();
            builder.Register<TransmitterAttackBuildService>(Lifetime.Singleton).AsSelf();
            builder.Register<TransmitterAttackService>(Lifetime.Singleton)
                .As<ITransmitterAttackService>()
                .AsSelf();
            builder.RegisterEntryPoint<TransmitterFireEffect>(Lifetime.Singleton).AsSelf();
            builder.Register<CombatPlaygroundSkillEffects>(Lifetime.Singleton)
                .As<ICentralSkillEffectService>()
                .As<ICentralSkillEffectResetService>()
                .AsSelf();
            builder.Register<SkillExecutor>(Lifetime.Singleton)
                .As<ISkillExecutionService>()
                .AsSelf();
            builder.RegisterEntryPoint<SkillActivationService>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<EnemySystem.Visual.EnemyVisualSystem>(Lifetime.Singleton).AsSelf();

            new GameLoopEffectInstaller(combatEffectCatalog, this).Install(builder);
        }

        /// <summary>实验场景不读取背包，只提供空的攻击修正来源。</summary>
        private sealed class CombatPlaygroundAttackModifiers : ITransmitterAttackModifierSource
        {
            /// <summary>保持发射器基础参数不变。</summary>
            public void CollectAttackModifiers(
                string portId,
                ref TransmitterAttackModifiers modifiers)
            {
            }
        }
    }
}
