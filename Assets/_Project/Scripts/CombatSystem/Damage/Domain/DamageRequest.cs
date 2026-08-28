using UnityEngine;

namespace CombatSystem
{
    public struct DamageRequest
    {
        public int AttackId;
        public int SourceId;
        public float BaseDamage;
        public DamageType DamageType;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public bool IsCritical;
    }
}
