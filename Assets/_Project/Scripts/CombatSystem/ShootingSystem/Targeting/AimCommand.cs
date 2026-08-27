using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>瞄准来源输出的统一命令，未来可由自动目标或鼠标生成。</summary>
    public struct AimCommand
    {
        public Vector3 Direction;
        public IDamageable Target;
        public bool HasTarget;
        public bool IsManual;
    }
}
