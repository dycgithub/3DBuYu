using System;
using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>复用投射物运行时状态对象，避免飞行过程中反复分配。</summary>
    public sealed class ProjectileRuntimePool : IDisposable
    {
        private readonly Queue<ProjectileRuntime> _available = new();

        /// <summary>租借并初始化一个投射物运行时状态。</summary>
        /// <param name="projectileData">投射物数据快照。</param>
        /// <param name="projectileViewInstance">绑定的视觉实例。</param>
        /// <returns>可用于模拟的投射物运行时状态。</returns>
        public ProjectileRuntime Rent(
            ProjectileData projectileData,
            UnityEngine.GameObject projectileViewInstance)
        {
            if (_available.Count == 0)
            {
                return new ProjectileRuntime(projectileData, projectileViewInstance);
            }

            ProjectileRuntime projectileRuntime = _available.Dequeue();
            projectileRuntime.Initialize(projectileData, projectileViewInstance);
            return projectileRuntime;
        }

        /// <summary>归还投射物运行时状态对象。</summary>
        /// <param name="projectileRuntime">要归还的投射物运行时状态。</param>
        public void Return(ProjectileRuntime projectileRuntime)
        {
            if (projectileRuntime == null)
            {
                return;
            }
            projectileRuntime.ResetForReuse();
            _available.Enqueue(projectileRuntime);
        }

        /// <summary>清空可复用状态对象队列。</summary>
        public void ClearAvailableRuntimes() => _available.Clear();

        /// <summary>释放对象池持有的状态引用。</summary>
        public void Dispose() => ClearAvailableRuntimes();
    }
}
