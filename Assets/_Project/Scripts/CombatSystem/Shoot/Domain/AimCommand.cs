using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>描述一次端口瞄准输入及其可选目标。</summary>
    public struct AimCommand
    {
        /// <summary>瞄准方向。</summary>
        public Vector3 AimDirection;
        /// <summary>当前锁定目标。</summary>
        public IDamageable AimTarget;
        /// <summary>指示是否存在有效锁定目标。</summary>
        public bool HasAimTarget;
        /// <summary>指示命令是否来自手动输入。</summary>
        public bool IsManualInput;
    }
}
