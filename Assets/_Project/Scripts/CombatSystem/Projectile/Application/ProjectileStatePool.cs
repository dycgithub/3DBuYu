using System;
using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>复用投射物状态对象，避免飞行过程中反复分配。</summary>
    public sealed class ProjectileStatePool : IDisposable
    {
        private readonly Queue<ProjectileState> _available = new();

        /// <summary>租借并初始化一个投射物状态。</summary>
        /// <param name="projectileData">投射物数据快照。</param>
        /// <param name="projectileViewInstance">绑定的视觉实例。</param>
        /// <returns>可用于模拟的投射物状态。</returns>
        public ProjectileState Rent(
            ProjectileData projectileData,
            UnityEngine.GameObject projectileViewInstance)
        {
            if (_available.Count == 0)
            {
                return new ProjectileState(projectileData, projectileViewInstance);
            }

            ProjectileState projectileState = _available.Dequeue();
            projectileState.Initialize(projectileData, projectileViewInstance);
            return projectileState;
        }

        /// <summary>归还投射物状态对象。</summary>
        /// <param name="projectileState">要归还的投射物状态。</param>
        public void Return(ProjectileState projectileState)
        {
            if (projectileState == null)
            {
                return;
            }
            projectileState.ResetForReuse();
            _available.Enqueue(projectileState);
        }

        /// <summary>清空可复用状态对象队列。</summary>
        public void ClearAvailableStates() => _available.Clear();

        /// <summary>释放对象池持有的状态引用。</summary>
        public void Dispose() => ClearAvailableStates();
    }
}
