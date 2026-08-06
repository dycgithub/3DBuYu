using System.Collections.Generic;
using UnityEngine;
using GameSystem;
using Services;
using VContainer;

namespace TurretSystem
{
    /// <summary>
    /// 碰撞时间惩罚组件。
    /// 挂在球壁或炮台上，当敌人接触时扣减剩余时间。
    /// 有短暂冷却防止同一敌人多次触发。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CollisionTimePenalty : MonoBehaviour
    {
        [Header("惩罚配置")]
        [Tooltip("每次碰撞扣减的时间（秒）。")]
        public float penaltyPerHit = 3f;

        [Tooltip("碰撞冷却时间（秒），防止同一敌人连续触发。")]
        public float hitCooldown = 1f;

        [Header("特效")]
        [Tooltip("碰撞特效名称（通过 EffectManager 播放）。")]
        public string hitEffectName = "PlayerDamage";

        // ── 内部 ──────────────────────────────────────────

        private readonly Dictionary<int, float> _enemyCooldowns = new Dictionary<int, float>();

        [Inject] private GameSystem.GameManager _gameManager;
        [Inject] private IEffectService _effectService;

        #region Unity 生命周期

        private void OnTriggerEnter(Collider other)
        {
            TryApplyPenalty(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryApplyPenalty(collision.collider);
        }

        #endregion

        #region 内部

        private void TryApplyPenalty(Collider other)
        {
            if (!other.CompareTag("Enemy")) return;

            int enemyId = other.GetInstanceID();
            if (_enemyCooldowns.TryGetValue(enemyId, out float lastHitTime))
            {
                if (Time.time - lastHitTime < hitCooldown) return;
            }
            _enemyCooldowns[enemyId] = Time.time;

            ApplyPenalty();
        }

        private void ApplyPenalty()
        {
            if (_gameManager == null || _gameManager.CurrentState != GameState.Playing) return;
            if (_gameManager.Timer == null) return;

            _gameManager.Timer.AddTimePenalty(penaltyPerHit);

            _effectService?.Play(hitEffectName, transform.position);
        }

        #endregion
    }
}
