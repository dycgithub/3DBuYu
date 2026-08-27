using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 统一伤害请求。射击系统只提交请求，不直接操作敌人生命值。
    /// </summary>
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
