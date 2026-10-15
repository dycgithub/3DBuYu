using EffectSystem;
using System.Collections.Generic;
using Services;
using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>
    /// 玩家碰撞反馈组件。
    /// 挂在玩家 Rigidbody 上，根据敌人根标签和接触坐标播放受击效果。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerCollisionFeedback : MonoBehaviour
    {
        [Tooltip("碰撞冷却时间（秒），防止同一敌人连续触发。")]
        public float hitCooldown = 1f;

        [Header("特效")]
        [Tooltip("碰撞特效键（通过 EffectManager 播放）。")]
        public EffectId hitEffect = EffectId.PlayerDamage;

        private readonly Dictionary<int, float> _enemyCooldowns = new();

        [Inject] private ICombatPhaseService _combatPhase;
        [Inject] private IEffectService _effectService;

        private void OnTriggerEnter(Collider other)
        {
            TryPlayHitEffect(other, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter(Collision collision)
        {
            Collider other = collision.collider;
            Vector3 hitPosition = collision.contactCount > 0
                ? collision.GetContact(0).point
                : other.ClosestPoint(transform.position);
            TryPlayHitEffect(other, hitPosition);
        }

        private void OnDisable()
        {
            _enemyCooldowns.Clear();
        }

        private void TryPlayHitEffect(Collider other, Vector3 hitPosition)
        {
            if (other == null || !IsEnemyCollider(other))
            {
                return;
            }

            int enemyId = other.transform.root.gameObject.GetInstanceID();
            if (_enemyCooldowns.TryGetValue(enemyId, out float lastHitTime) &&
                Time.time - lastHitTime < Mathf.Max(0f, hitCooldown))
            {
                return;
            }

            _enemyCooldowns[enemyId] = Time.time;
            PlayHitEffect(hitPosition);
        }

        private static bool IsEnemyCollider(Collider collider)
        {
            return collider.CompareTag("Enemy") || collider.transform.root.CompareTag("Enemy");
        }

        private void PlayHitEffect(Vector3 hitPosition)
        {
            if (_combatPhase == null || !_combatPhase.CanPerformCombatActions)
            {
                return;
            }

            _effectService?.Play(hitEffect, hitPosition);
        }
    }
}
