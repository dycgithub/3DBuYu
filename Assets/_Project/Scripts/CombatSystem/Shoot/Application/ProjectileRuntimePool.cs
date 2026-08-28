using System;
using System.Collections.Generic;

namespace CombatSystem
{
    public sealed class ProjectileRuntimePool : IDisposable
    {
        private readonly Queue<ProjectileRuntime> _available = new();

        public ProjectileRuntime Rent(ProjectileInfo info, UnityEngine.GameObject view)
        {
            if (_available.Count == 0)
                return new ProjectileRuntime(info, view);

            ProjectileRuntime runtime = _available.Dequeue();
            runtime.Initialize(info, view);
            return runtime;
        }

        public void Return(ProjectileRuntime runtime)
        {
            if (runtime == null)
                return;
            runtime.Reset();
            _available.Enqueue(runtime);
        }

        public void Clear() => _available.Clear();

        public void Dispose() => Clear();
    }
}
