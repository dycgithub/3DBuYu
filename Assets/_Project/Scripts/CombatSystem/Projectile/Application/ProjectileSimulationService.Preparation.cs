using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    public sealed partial class ProjectileSimulationService
    {
        /// <summary>准备全部普通弹体及配置快照；任何失败均释放本批次，不公开半批子弹。</summary>
        public bool TryPrepare(IReadOnlyList<ProjectileData> data, out IPreparedProjectileBatch batch)
        {
            batch = null;
            if (data == null || data.Count == 0)
            {
                return false;
            }
            foreach (ProjectileData projectile in data)
            {
                ProjectileProfileSO profile = projectile.ProjectileProfile;
                if (profile == null || profile.TrajectoryDefinition == null ||
                    profile.TrajectoryDefinition.IsHitscan || profile.EmitProjectilesSequentially ||
                    projectile.MaximumTravelDistance <= 0f || projectile.SpeedUnitsPerSecond <= 0f ||
                    projectile.DamageAmount < 0f)
                {
                    return false;
                }
            }

            var prepared = new PreparedBatch(this);
            try
            {
                foreach (ProjectileData projectile in data)
                {
                    ProjectileState state = SpawnSingleProjectile(in projectile);
                    if (state == null)
                    {
                        return false;
                    }
                    prepared.States.Add(state);
                    if (_viewPool != null && projectile.ProjectileProfile.VisualDefinition != null &&
                        state.ViewInstance == null)
                    {
                        return false;
                    }
                    state.ViewInstance?.SetActive(false);
                }
                _activeProjectiles.Capacity = Mathf.Max(_activeProjectiles.Capacity,
                    _activeProjectiles.Count + prepared.States.Count);
                batch = prepared;
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[ProjectileSimulation] 准备投射物失败: {exception.Message}");
                return false;
            }
            finally
            {
                if (batch == null)
                {
                    prepared.Dispose();
                }
            }
        }

        /// <summary>保存已租借的完整批次；提交无再次生成或拒绝点，取消归还全部资源。</summary>
        private sealed class PreparedBatch : IPreparedProjectileBatch
        {
            private readonly ProjectileSimulationService _owner;
            internal readonly List<ProjectileState> States = new();
            private bool _committed;

            internal PreparedBatch(ProjectileSimulationService owner) => _owner = owner;

            public void Commit()
            {
                if (_committed)
                {
                    return;
                }
                _owner._activeProjectiles.AddRange(States);
                _committed = true;
                foreach (ProjectileState state in States)
                {
                    state.ViewInstance?.SetActive(true);
                }
                States.Clear();
                _owner.PublishSpatialSnapshot();
            }

            public void Dispose()
            {
                foreach (ProjectileState state in States)
                {
                    _owner.ReleaseProjectile(state);
                }
                States.Clear();
                _committed = true;
            }
        }
    }
}
