using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    public struct AimCommand
    {
        public Vector3 Direction;
        public IDamageable Target;
        public bool HasTarget;
        public bool IsManual;
    }
}
