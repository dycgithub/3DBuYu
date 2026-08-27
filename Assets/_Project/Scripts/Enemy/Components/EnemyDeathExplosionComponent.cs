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

        [Tooltip("爆炸范围检测的可复用碰撞体缓冲区容量")]
        [SerializeField, Min(1)] private int overlapBufferCapacity = 32;

        [Header("调试")]
        [SerializeField] private bool showGizmo = true;

        [Inject] private IEffectService _effectService;
        [Inject] private GameManager _gameManager;

        private Enemy _enemy;
        private Collider[] _overlapBuffer;
        private bool subscribed;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _overlapBuffer = new Collider[Mathf.Max(1, overlapBufferCapacity)];
        }

        private void OnEnable()
        {
            if (_overlapBuffer == null)
                _overlapBuffer = new Collider[Mathf.Max(1, overlapBufferCapacity)];

            _enemy ??= GetComponent<Enemy>();
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
            // 范围检测玩家。死亡高频发生时复用缓冲区，避免 OverlapSphere 分配数组。
            int hitCount = QueryOverlappingColliders();
            bool hitPlayer = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                if (hit != null && hit.CompareTag("Player"))
                {
                    hitPlayer = true;
                    break;
                }
            }

            if (hitPlayer && _gameManager != null && _gameManager.CurrentState == GameState.Playing)
                _gameManager.Timer?.AddTimePenalty(timePenalty);

            _effectService?.Play("BigExplosion", transform.position);
        }

        private int QueryOverlappingColliders()
        {
            int hitCount;
            do
            {
                hitCount = Physics.OverlapSphereNonAlloc(
                    transform.position,
                    explosionRadius,
                    _overlapBuffer,
                    Physics.AllLayers,
                    QueryTriggerInteraction.UseGlobal);

                if (hitCount < _overlapBuffer.Length)
                    return hitCount;

                System.Array.Resize(ref _overlapBuffer, _overlapBuffer.Length * 2);
            }
            while (true);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmo) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
