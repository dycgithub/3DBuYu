using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 单个子弹的生成参数。ProjectileRuntime 会复制该数据并独立运行。
    /// </summary>
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
