using CombatSystem;
using EffectSystem;
using EnemySystem;
using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;
using _Project.Tests.CombatPlayground.View;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>
    /// 注册 CombatPlayground 的场景组件和本地战斗边界。
    /// </summary>
    public sealed class CombatPlaygroundComposition : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Enemy target;
        [SerializeField] private Transform firePoint;
        [SerializeField] private Play.TransmitterSO transmitter;
        [SerializeField] private ProjectileProfileSO defaultProjectile;
        [SerializeField] private BuffDefinitionSO buffDefinition;
        [SerializeField] private EffectManager effectManager;
        [SerializeField] private CombatPlaygroundController controller;
        [SerializeField] private CombatPlaygroundView view;
        [SerializeField] private CombatPlaygroundJuice juice;

        /// <summary>注册场景组件和实验场景控制对象。</summary>
        public void Register(IContainerBuilder builder)
        {
            if (sceneCamera == null || target == null || firePoint == null ||
                transmitter == null || defaultProjectile == null ||
                effectManager == null ||
                controller == null || view == null || juice == null)
            {
                throw new System.InvalidOperationException(
                    "CombatPlaygroundComposition 缺少场景组件或默认配置。");
            }

            builder.RegisterComponent(sceneCamera).AsSelf();
            builder.RegisterComponent(target).AsSelf();
            builder.Register<SpatialQueryService>(Lifetime.Scoped)
                .As<ISpatialQueryService>()
                .As<IProjectileQueryService>()
                .As<IProjectileSnapshotPublisher>()
                .AsSelf();
            builder.RegisterComponent(effectManager)
                .As<Services.IEffectService>()
                .As<Services.IPooledEffectService>()
                .AsSelf();
            builder.RegisterComponent(controller).AsSelf();
            builder.RegisterComponent(view).AsSelf();
            builder.RegisterComponent(juice).AsSelf();
        }

        /// <summary>获取场景默认发射器配置。</summary>
        public Play.TransmitterSO Transmitter => transmitter;

        /// <summary>获取场景默认投射物配置。</summary>
        public ProjectileProfileSO DefaultProjectile => defaultProjectile;

        /// <summary>获取场景配置的 Buff。</summary>
        public BuffDefinitionSO BuffDefinition => buffDefinition;
    }
}
