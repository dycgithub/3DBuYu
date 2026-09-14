using CombatSystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证投射物命中请求使用当前飞行方向，而不是初始发射方向。
    /// </summary>
    public sealed class ProjectileImpactDirectionTests
    {
        private GameObject _targetObject;
        private ProjectileProfileSO _profile;

        [TearDown]
        public void TearDown()
        {
            if (_profile != null)
            {
                Object.DestroyImmediate(_profile.TrajectoryDefinition);
                Object.DestroyImmediate(_profile);
            }

            if (_targetObject != null)
            {
                Object.DestroyImmediate(_targetObject);
            }
        }

        [Test]
        public void BoomerangHit_UsesReturnDirectionForKnockback()
        {
            _profile = ProjectileProfileSO.CreateRuntimeProfile("BoomerangImpactProfile");
            _profile.SpeedUnitsPerSecond = 1f;
            _profile.MaximumTravelDistance = 2f;
            _profile.TrajectoryDefinition = ScriptableObject.CreateInstance<BoomerangTrajectoryDefinitionSO>();

            _targetObject = new GameObject("ImpactTarget");
            TestDamageTarget target = _targetObject.AddComponent<TestDamageTarget>();
            RecordingCollisionQuery collisionQuery = new(target);
            RecordingEffectDispatcher effectDispatcher = new();
            ProjectileSimulationService simulation = new(
                new ProjectileViewPool(null),
                new ProjectileRuntimePool(),
                collisionQuery,
                new RecordingDamageProcessor(),
                effectDispatcher);
            ProjectileData projectile = new()
            {
                ProjectileProfile = _profile,
                OriginPosition = Vector3.zero,
                LaunchDirection = Vector3.forward,
                DamageAmount = 1f,
                SpeedUnitsPerSecond = 1f,
                MaximumTravelDistance = 2f,
                CollisionRadius = 0.1f
            };

            try
            {
                Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
                simulation.SimulateStep(1f);
                simulation.SimulateStep(1f);
                simulation.SimulateStep(0.1f);

                Assert.That(effectDispatcher.LastRequest.ImpactDirection.z, Is.LessThan(0f));
            }
            finally
            {
                simulation.Dispose();
            }
        }

        private sealed class TestDamageTarget : MonoBehaviour, IDamageable
        {
            public Vector3 Position => transform.position;
            public bool IsAlive => true;
            public Transform Transform => transform;
        }

        private sealed class RecordingCollisionQuery : IProjectileCollisionQuery
        {
            private readonly IDamageable _target;
            private int _queryCount;

            public RecordingCollisionQuery(IDamageable target)
            {
                _target = target;
            }

            public bool TryFindSweepCollision(
                ProjectileRuntime projectile,
                float travelDistance,
                out RaycastHit hit,
                out IDamageable target)
            {
                _queryCount++;
                hit = default;
                target = _queryCount >= 3 ? _target : null;
                return target != null;
            }

            public bool TryFindRaycastCollision(
                ProjectileData projectileData,
                out RaycastHit hit,
                out IDamageable target)
            {
                hit = default;
                target = null;
                return false;
            }
        }

        private sealed class RecordingDamageProcessor : IDamageRequestProcessor
        {
            public bool TryApplyDamage(
                IDamageable target,
                in DamageRequest request,
                out DamageResult result)
            {
                result = new DamageResult
                {
                    Outcome = DamageOutcome.Applied,
                    AppliedDamage = request.DamageAmount
                };
                return true;
            }
        }

        private sealed class RecordingEffectDispatcher :
            IProjectileEffectDispatcher,
            IProjectileStateEffectDispatcher
        {
            public DamageRequest LastRequest { get; private set; }

            public void DispatchHitEffects(
                ProjectileProfileSO projectileProfile,
                DamageRequest request,
                DamageResult result,
                IDamageable target)
            {
                LastRequest = request;
            }

            public void DispatchHitEffects(
                ProjectileRuntime projectile,
                ProjectileProfileSO projectileProfile,
                DamageRequest request,
                DamageResult result,
                IDamageable target)
            {
                LastRequest = request;
            }

            public void DispatchBeforeHitEffects(
                ProjectileProfileSO projectileProfile,
                DamageRequest request,
                IDamageable target)
            {
            }

            public void DispatchExpiredEffects(ProjectileProfileSO projectileProfile, Vector3 position)
            {
            }
        }
    }
}
