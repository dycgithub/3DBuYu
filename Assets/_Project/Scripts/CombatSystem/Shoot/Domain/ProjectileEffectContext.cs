using UnityEngine;
using Services;

namespace CombatSystem
{
    /// <summary>
    /// 为投射物效果提供来源、目标、位置和结果信息。
    /// </summary>
    public struct ProjectileEffectContext
    {
        /// <summary>发起攻击的对象实例 ID。</summary>
        public int SourceInstanceId;
        /// <summary>命中目标实例 ID。</summary>
        public int TargetInstanceId;
        /// <summary>命中目标的 GameObject。</summary>
        public GameObject TargetGameObject;
        /// <summary>指示命中是否导致目标死亡。</summary>
        public bool WasKilled;
        /// <summary>命中或过期位置。</summary>
        public Vector3 HitPosition;
        /// <summary>实际应用的伤害量。</summary>
        public float AppliedDamage;
        /// <summary>产生当前效果的投射物配置。</summary>
        public ProjectileProfileSO ProjectileProfile;
        /// <summary>产生当前效果的原始伤害请求。</summary>
        public DamageRequest DamageRequest;
        /// <summary>用于范围效果提交伤害的统一处理器。</summary>
        public IDamageRequestProcessor DamageProcessor;
        /// <summary>用于范围效果获取目标的空间查询服务。</summary>
        public ISpatialQueryService SpatialQueryService;
        /// <summary>当前投射物的可变状态；Hitscan 没有该状态。</summary>
        public ProjectileRuntime ProjectileState;
    }
}
