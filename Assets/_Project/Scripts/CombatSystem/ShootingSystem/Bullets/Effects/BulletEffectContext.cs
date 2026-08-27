using Unity.Mathematics;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>效果执行时需要的命中快照，不保存对 ECS 的依赖。</summary>
    public struct BulletEffectContext
    {
        public int SourceId;
        public int TargetInstanceId;
        public GameObject TargetObject;
        public bool IsKill;
        public float3 HitPoint;
        public float Damage;
        public BulletProfile Profile;
    }
}
