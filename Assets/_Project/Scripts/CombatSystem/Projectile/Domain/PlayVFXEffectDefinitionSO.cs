using EffectSystem;
using Services;
using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>在命中位置或命中目标处播放一个配置的视觉效果。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Effects/Play VFX")]
    public sealed class PlayVFXEffectDefinitionSO : ProjectileEffectDefinitionSO
    {
        /// <summary>要播放的效果标识。</summary>
        [FormerlySerializedAs("Effect")]
        public EffectId EffectId = EffectId.None;
        /// <summary>指示效果是否挂到命中目标下。</summary>
        [FormerlySerializedAs("AttachToTarget")]
        public bool AttachEffectToTarget;

        /// <summary>按配置位置播放一次效果。</summary>
        public override void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService)
        {
            if (EffectId == EffectId.None || effectService == null)
            {
                return;
            }

            Transform parent = null;
            Vector3 position = context.HitPosition;
            if (AttachEffectToTarget && context.TargetGameObject != null)
            {
                parent = context.TargetGameObject.transform;
                position = parent.position;
            }

            effectService.Play(EffectId, position, parent);
        }
    }
}
