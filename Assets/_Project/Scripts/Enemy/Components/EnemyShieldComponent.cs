using UnityEngine;
using VContainer;
using Services;
using Utils;

namespace EnemySystem.Components
{
    /// <summary>
    /// 坦克敌人护盾行为(Tank)。
    /// 吸收传入伤害,停止受击后自动恢复。
    /// 挂到 Tank 类型敌人预制体上。
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyShieldComponent : MonoBehaviour, IPooledObject
    {
        [Header("护盾")]
        [Tooltip("护盾最大值")]
        [SerializeField] private float shieldValue = 50f;

        [Tooltip("护盾恢复速度(点/秒)")]
        [SerializeField] private float shieldRegenRate = 5f;

        [Tooltip("受击后多少秒开始恢复")]
        [SerializeField] private float shieldRegenDelay = 3f;

        [Inject] private IEffectService _effectService;

        private Enemy _enemy;
        private float currentShield;
        private float lastDamageTime;
        private bool subscribed;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            currentShield = shieldValue;
            lastDamageTime = -shieldRegenDelay;
        }

        private void OnEnable()
        {
            if (_enemy != null && !subscribed)
            {
                _enemy.RegisterPreDamageInterceptor(HandlePreDamage);
                subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_enemy != null && subscribed)
            {
                _enemy.UnregisterPreDamageInterceptor(HandlePreDamage);
                subscribed = false;
            }
        }

        private bool HandlePreDamage(Enemy enemy, float originalDamage, ref float finalDamage)
        {
            if (currentShield <= 0f) return true;

            lastDamageTime = Time.time;

            float absorb = Mathf.Min(currentShield, finalDamage);
            currentShield -= absorb;
            finalDamage -= absorb;

            _effectService?.Play(EffectSystem.EffectId.ShieldHit, transform.position);

            return true; // 继续(可能仍有溢出伤害到 HP)
        }

        private void Update()
        {
            if (currentShield >= shieldValue) return;
            if (Time.time - lastDamageTime < shieldRegenDelay) return;

            currentShield = Mathf.Min(shieldValue, currentShield + shieldRegenRate * Time.deltaTime);
        }

        public void OnRentFromPool()
        {
            currentShield = shieldValue;
            lastDamageTime = -shieldRegenDelay;
        }

        public void OnReturnToPool()
        {
            currentShield = 0f;
            lastDamageTime = 0f;
        }
    }
}
