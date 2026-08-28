using UnityEngine;

namespace CombatSystem
{
    public struct ProjectileInfo
    {
        public int ProjectileId;
        public int AttackId;
        public int SourceId;
        public BulletProfile Profile;
        public Vector3 Origin;
        public Vector3 Direction;
        public float Damage;
        public float Speed;
        public float MaxDistance;
        public float Radius;
        public int Penetration;
        public DamageType DamageType;
        public bool IsCritical;
    }
}
