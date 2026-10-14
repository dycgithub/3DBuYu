using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>从中央切平面同时环形发射固定百颗独立追踪弹，只有伤害应用物品强度倍率。</summary>
    [CreateAssetMenu(menuName = "Combat/Ability Actions/Star Cluster")]
    public sealed class StarClusterActionSO : SkillActionDefinitionSO
    {
        private const int ProjectileCount = 100;
        private const float BaseDamage = 10f;
        [SerializeField] private ProjectileProfileSO projectileProfile;

        /// <summary>校验追踪配置和目标；来源消费前准备完整百发，消费失败自动取消。</summary>
        public override bool TryExecute(
            in SkillExecutionContext context,
            IReadOnlyList<IDamageable> targets,
            IDamageRequestProcessor damageProcessor,
            out int appliedTargetCount)
        {
            appliedTargetCount = 0;
            if (context.Projectiles == null || projectileProfile == null ||
                !(projectileProfile.TrajectoryDefinition is HomingTrajectoryDefinitionSO) ||
                projectileProfile.EmitProjectilesSequentially || !HasLivingTarget(targets))
            {
                return false;
            }

            var requests = BuildRequests(in context);
            IProjectileLauncher launcher = context.Projectiles;
            if (damageProcessor is SkillDamageBatch batch)
            {
                if (launcher is IProjectileBatchPreparer preparer)
                {
                    if (!preparer.TryPrepare(requests, out IPreparedProjectileBatch prepared))
                    {
                        return false;
                    }
                    batch.DeferProjectiles(prepared);
                    return true;
                }
                if (batch.RequiresProjectilePreparation)
                {
                    return false;
                }
                // 独立适配器没有来源消费；拒绝必须传回批次调用者，不能被 Action 吞掉。
                batch.DeferAction(() =>
                {
                    if (!Emit(launcher, requests))
                    {
                        throw new System.InvalidOperationException("独立星群发射适配器拒绝了请求。");
                    }
                });
                return true;
            }

            if (launcher is IProjectileBatchPreparer direct)
            {
                if (!direct.TryPrepare(requests, out IPreparedProjectileBatch prepared))
                {
                    return false;
                }
                using (prepared)
                {
                    prepared.Commit();
                }
                return true;
            }

            return Emit(launcher, requests);
        }

        /// <summary>在中央所在球面切平面上等角度展开，所有弹共享出生点但不共享目标。</summary>
        private ProjectileLaunchRequest[] BuildRequests(in SkillExecutionContext context)
        {
            Vector3 normal = context.SourcePosition.sqrMagnitude > 0.0001f
                ? context.SourcePosition.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal,
                Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.forward).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            var requests = new ProjectileLaunchRequest[ProjectileCount];
            for (int index = 0; index < ProjectileCount; index++)
            {
                float angle = index * (2f * Mathf.PI / ProjectileCount);
                Vector3 direction = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                requests[index] = new ProjectileLaunchRequest(
                    context.SourceInstanceId, projectileProfile, context.SourcePosition,
                    direction, BaseDamage * context.EffectScale);
            }
            return requests;
        }

        /// <summary>同步提交同帧全部单弹请求，避免通用散射批次给起点增加横向偏移。</summary>
        private static bool Emit(IProjectileLauncher launcher, ProjectileLaunchRequest[] requests)
        {
            foreach (ProjectileLaunchRequest request in requests)
            {
                if (!launcher.Launch(in request))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>拒绝空快照及已死亡或失活的目标。</summary>
        private static bool HasLivingTarget(IReadOnlyList<IDamageable> targets)
        {
            if (targets != null)
            {
                foreach (IDamageable target in targets)
                {
                    if (target.IsAliveAndValid())
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
