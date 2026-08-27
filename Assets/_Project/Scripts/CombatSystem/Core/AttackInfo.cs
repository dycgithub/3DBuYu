using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 一次攻击的运行时快照。资产配置不会在攻击执行过程中被修改。
    /// </summary>
    public struct AttackInfo
    {
        public int AttackId;
        public int SourceId;
        public int PortIndex;
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
