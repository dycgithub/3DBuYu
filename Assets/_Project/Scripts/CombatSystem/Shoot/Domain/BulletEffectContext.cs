using UnityEngine;

namespace CombatSystem
{
    public struct BulletEffectContext
    {
        public int SourceId;
        public int TargetInstanceId;
        public GameObject TargetObject;
        public bool IsKill;
        public Vector3 HitPoint;
        public float Damage;
        public BulletProfile Profile;
    }
}
