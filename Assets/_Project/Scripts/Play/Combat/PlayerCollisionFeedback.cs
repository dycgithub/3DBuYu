using System.Collections.Generic;
using UnityEngine;
using Services;
using VContainer;
using EffectSystem;

namespace Play
{
    /// <summary>
    /// 玩家碰撞反馈组件。
    /// 挂在球壁或炮台上，当敌人接触时播放一次受击效果。
    /// 有短暂冷却防止同一敌人连续触发。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayerCollisionFeedback : MonoBehaviour
    {
        [Tooltip("碰撞冷却时间（秒），防止同一敌人连续触发。")]
        public float hitCooldown = 1f;

        [Header("特效")]
        [Tooltip("碰撞特效键（通过 EffectManager 播放）。")]
        public EffectId hitEffect = EffectId.PlayerDamage;

        // ── 内部 ──────────────────────────────────────────

        private readonly Dictionary<int, float> _enemyCooldowns = new Dictionary<int, float>();

        [Inject] private ICombatPhaseService _combatPhase;
        [Inject] private IEffectService _effectService;

        #region Unity 生命周期

        private void OnTriggerEnter(Collider other)
        {
            TryPlayHitEffect(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryPlayHitEffect(collision.collider);
        }

        #endregion

        #region 内部

        private void TryPlayHitEffect(Collider other)
        {
            if (!other.CompareTag("Enemy"))
            {
                return;
            }

            int enemyId = other.GetInstanceID();
            if (_enemyCooldowns.TryGetValue(enemyId, out float lastHitTime))
            {
                if (Time.time - lastHitTime < hitCooldown)
                {
                    return;
                }
            }
            _enemyCooldowns[enemyId] = Time.time;

            PlayHitEffect();
        }

        private void PlayHitEffect()
        {
            if (_combatPhase == null || !_combatPhase.CanPerformCombatActions)
            {
                return;
            }

            _effectService?.Play(hitEffect, transform.position);
        }

        #endregion
    }
}
