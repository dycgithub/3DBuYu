using UnityEngine;

namespace CombatSystem
{
    public struct AttackInfo
    {
        public int AttackId;
        public int SourceId;
        public int TransmitterIndex;
        public BulletProfile Profile;
        public Vector3 Origin;
        public Vector3 Direction;
        public float Damage;
        public float EnergyCost;
        public int ProjectileCount;
        public int Penetration;
        public DamageType DamageType;
        public float Speed;
        public float MaxDistance;
        public float Radius;
        public bool IsCritical;
    }
}
