using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 逻辑子弹对象池。视觉对象和逻辑状态分开回收，避免每发子弹都 new HashSet 和运行时对象。
    /// </summary>
    public sealed class ProjectileRuntimePool : IDisposable
    {
        private readonly Queue<ProjectileRuntime> _available = new();

        public ProjectileRuntime Rent(ProjectileInfo info, GameObject view)
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

        public void Clear()
        {
            _available.Clear();
        }

        public void Dispose() => Clear();
    }
}
