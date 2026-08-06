using UnityEngine;
using VContainer;
using Services;

namespace EnemySystem.Components
{
    /// <summary>
    /// 快速敌人闪避行为(Fast)。
    /// 概率闪避传入伤害,闪避成功时瞬间位移。
    /// 挂到 Fast 类型敌人预制体上。
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyDodgeComponent : MonoBehaviour
    {
        [Header("闪避")]
        [Tooltip("闪避概率 0-1")]
        [Range(0f, 1f)]
        [SerializeField] private float dodgeChance = 0.3f;

        [Tooltip("闪避位移距离")]
        [SerializeField] private float dodgeDistance = 2f;

        [Inject] private IEffectService _effectService;

        private Enemy _enemy;
        private bool _subscribed;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        private void OnEnable()
        {
            if (_enemy != null && !_subscribed)
            {
                _enemy.OnPreDamage += HandlePreDamage;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_enemy != null && _subscribed)
            {
                _enemy.OnPreDamage -= HandlePreDamage;
                _subscribed = false;
            }
        }

        private bool HandlePreDamage(Enemy enemy, float originalDamage, ref float finalDamage)
        {
            if (Random.value >= dodgeChance) return true; // 不闪避

            PerformDodge();
            return false; // 完全闪避
        }

        private void PerformDodge()
        {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            dir.Normalize();

            transform.position += dir * dodgeDistance;
            _effectService?.Play("EnemyDodge", transform.position);
        }
    }
}
