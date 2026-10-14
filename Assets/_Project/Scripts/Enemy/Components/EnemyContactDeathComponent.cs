using UnityEngine;

namespace EnemySystem.Components
{
    /// <summary>为指定敌人提供可配置的玩家接触死亡行为。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyContactDeathComponent : MonoBehaviour
    {
        [SerializeField] private bool _killOnPlayerContact = true;

        private Enemy _enemy;

        /// <summary>缓存所属敌人。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        /// <summary>开关启用且宿主存活时，以玩家接触原因结束敌人生命。</summary>
        internal void TryKillOnPlayerContact()
        {
            if (!_killOnPlayerContact || !isActiveAndEnabled)
            {
                return;
            }

            _enemy ??= GetComponent<Enemy>();
            if (_enemy != null && !_enemy.IsDead)
            {
                _enemy.DieFromHealth(EnemyDeathCause.PlayerContact);
            }
        }
    }
}
