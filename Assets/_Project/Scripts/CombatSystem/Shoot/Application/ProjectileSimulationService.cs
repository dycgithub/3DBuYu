using System;
using System.Collections.Generic;
using Interfaces;
using UnityEngine;
using VContainer.Unity;

namespace CombatSystem
{
    public sealed class ProjectileSimulationService : ITickable, IDisposable
    {
        private readonly ProjectilePool _viewPool;
        private readonly ProjectileRuntimePool _runtimePool;
        private readonly IProjectileHitQuery _hitQuery;
        private readonly IDamageApplier _damage;
        private readonly IBulletEffectDispatcher _effects;
        private readonly List<ProjectileRuntime> _active = new();
        private int _nextProjectileId;

        public int ActiveCount => _active.Count;

        public ProjectileSimulationService(
            ProjectilePool viewPool,
            ProjectileRuntimePool runtimePool,
            IProjectileHitQuery hitQuery,
            IDamageApplier damage,
            IBulletEffectDispatcher effects)
        {
            _viewPool = viewPool;
            _runtimePool = runtimePool;
            _hitQuery = hitQuery;
            _damage = damage;
            _effects = effects;
        }

        public bool TrySpawn(in ProjectileInfo info) => TrySpawnBatch(in info, 1);

        public bool TrySpawnBatch(in ProjectileInfo info, int count)
        {
            if (count <= 0 || info.Profile == null || info.Damage < 0f)
                return false;
            for (int i = 0; i < count; i++)
                SpawnSingle(in info);
            return true;
        }

        private void SpawnSingle(in ProjectileInfo info)
        {
            Vector3 direction = info.Direction.sqrMagnitude > 0.0001f
                ? info.Direction.normalized
                : Vector3.forward;
            ProjectileInfo normalized = info;
            normalized.ProjectileId = ++_nextProjectileId;
            normalized.Direction = direction;

            if (normalized.Profile.Trajectory != null && normalized.Profile.Trajectory.IsHitscan)
            {
                ResolveHitscan(normalized);
                return;
            }

            GameObject view = _viewPool?.Rent(
                normalized.Profile,
                normalized.Origin,
                Quaternion.LookRotation(direction, Vector3.up));
            ProjectileRuntime runtime = _runtimePool.Rent(normalized, view);
            _active.Add(runtime);
        }

        public void Tick() => Step(Time.deltaTime);

        public void Step(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ProjectileRuntime projectile = _active[i];
                Simulate(projectile, deltaTime);
                if (!projectile.IsComplete)
                    continue;
                Release(projectile);
                _active.RemoveAt(i);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _active.Count; i++)
                Release(_active[i]);
            _active.Clear();
            _viewPool?.Clear();
            _runtimePool?.Clear();
        }

        private void Simulate(ProjectileRuntime projectile, float deltaTime)
        {
            if (projectile.IsComplete)
                return;

            float travelDistance = projectile.Info.Speed * deltaTime;
            if (travelDistance <= 0f)
            {
                projectile.RemainingLife -= deltaTime;
                if (projectile.RemainingLife <= 0f)
                    CompleteExpired(projectile);
                return;
            }

            Vector3 direction = projectile.Info.Profile.Trajectory != null
                ? projectile.Info.Profile.Trajectory.GetDirection(projectile, projectile.Direction).normalized
                : projectile.Direction;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;
            projectile.Direction = direction;

            if (_hitQuery != null && _hitQuery.TrySweep(projectile, travelDistance, out RaycastHit hit, out IDamageable target))
            {
                projectile.Position = hit.point;
                projectile.TraveledDistance += hit.distance;
                ApplyHit(projectile, target, hit.point, hit.normal);
                if (!projectile.IsComplete)
                    projectile.Position += direction * 0.01f;
            }
            else
            {
                projectile.Position += direction * travelDistance;
                projectile.TraveledDistance += travelDistance;
            }

            projectile.RemainingLife -= deltaTime;
            UpdateView(projectile);
            if (!projectile.IsComplete &&
                (projectile.TraveledDistance >= projectile.Info.MaxDistance || projectile.RemainingLife <= 0f))
                CompleteExpired(projectile);
        }

        private void ResolveHitscan(ProjectileInfo info)
        {
            if (_hitQuery == null || !_hitQuery.TryRaycast(info, out RaycastHit hit, out IDamageable target))
            {
                _effects?.DispatchExpired(info.Profile, info.Origin + info.Direction * info.MaxDistance);
                return;
            }

            DamageRequest request = CreateDamageRequest(info, hit.point, hit.normal);
            _effects?.DispatchTrigger(info.Profile, request, target);
            DamageResult result = default;
            _damage?.TryApply(target, request, out result);
            _effects?.DispatchHit(info.Profile, request, result, target);
        }

        private void ApplyHit(ProjectileRuntime projectile, IDamageable target, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (projectile.Info.Penetration > 0)
            {
                int targetId = target.Transform != null ? target.Transform.GetInstanceID() : 0;
                if (targetId != 0 && !projectile.HitTargetIds.Add(targetId))
                    return;
            }

            DamageRequest request = CreateDamageRequest(projectile.Info, hitPoint, hitNormal);
            _effects?.DispatchTrigger(projectile.Info.Profile, request, target);
            DamageResult result = default;
            _damage?.TryApply(target, request, out result);
            _effects?.DispatchHit(projectile.Info.Profile, request, result, target);

            if (projectile.RemainingPenetration > 0)
            {
                projectile.RemainingPenetration--;
                return;
            }
            projectile.IsComplete = true;
        }

        private static DamageRequest CreateDamageRequest(ProjectileInfo info, Vector3 hitPoint, Vector3 hitNormal)
        {
            return new DamageRequest
            {
                AttackId = info.AttackId,
                SourceId = info.SourceId,
                BaseDamage = info.Damage,
                DamageType = info.DamageType,
                HitPoint = hitPoint,
                HitNormal = hitNormal,
                IsCritical = info.IsCritical
            };
        }

        private void CompleteExpired(ProjectileRuntime projectile)
        {
            if (projectile.IsComplete)
                return;
            projectile.IsComplete = true;
            _effects?.DispatchExpired(projectile.Info.Profile, projectile.Position);
        }

        private static void UpdateView(ProjectileRuntime projectile)
        {
            if (projectile.View != null)
                projectile.View.transform.SetPositionAndRotation(
                    projectile.Position,
                    Quaternion.LookRotation(projectile.Direction, Vector3.up));
        }

        private void Release(ProjectileRuntime projectile)
        {
            _viewPool?.Return(projectile.Info.Profile, projectile.View);
            _runtimePool?.Return(projectile);
        }
    }
}
