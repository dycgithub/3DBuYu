using UnityEngine;
using VContainer;
using Services;
using GameSystem;

namespace EnemySystem.Components
{
    /// <summary>
    /// 坦克敌人死亡爆炸行为(Tank)。
    /// 死亡时对周围 Player 触发时间惩罚。
    /// 挂到 Tank 类型敌人预制体上。
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyDeathExplosionComponent : MonoBehaviour
    {
        [Header("爆炸")]
        [Tooltip("爆炸时间惩罚(秒)")]
        [SerializeField] private float timePenalty = 5f;

        [Tooltip("爆炸检测半径")]
        [SerializeField] private float explosionRadius = 5f;

        [Header("调试")]
        [SerializeField] private bool showGizmo = true;

        [Inject] private IEffectService _effectService;
        [Inject] private GameManager _gameManager;

        private Enemy _enemy;
        private bool subscribed;

        private void OnEnable()
        {
            _enemy = GetComponent<Enemy>();
            if (_enemy != null && !subscribed)
            {
                _enemy.OnDied += HandleDeath;
                subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_enemy != null && subscribed)
            {
                _enemy.OnDied -= HandleDeath;
                subscribed = false;
            }
        }

        private void HandleDeath(Enemy enemy)
        {
            // 范围检测玩家
            var hits = Physics.OverlapSphere(transform.position, explosionRadius);
            bool hitPlayer = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] != null && hits[i].CompareTag("Player"))
                {
                    hitPlayer = true;
                    break;
                }
            }

            if (hitPlayer && _gameManager != null && _gameManager.CurrentState == GameState.Playing)
                _gameManager.Timer?.AddTimePenalty(timePenalty);

            _effectService?.Play("BigExplosion", transform.position);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmo) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
