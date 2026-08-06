using Unity.Entities;
using Unity.Mathematics;

namespace ShootingSystem
{
    public struct SpawnRequest
    {
        public BulletProfile Profile;
        public Entity Owner;
        public float3 Origin;
        public float3 Direction;
        public float ChargeRatio;
        public int Seed;

        /// <summary>伤害覆写(0 = 使用 Profile 默认伤害)。由端口弹药攻击加成注入。</summary>
        public float DamageOverride;
    }
}
