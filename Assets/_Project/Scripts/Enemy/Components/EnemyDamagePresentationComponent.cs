using EffectSystem;
using FlockingSystem.ECS;
using Services;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 敌人的命中闪白、死亡高亮和反馈特效组件。
    /// <para>表现状态与生命规则分离，死亡高亮在对象池回收前持续指定时长。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyDamagePresentationComponent : MonoBehaviour, IPooledObject
    {
        [Header("命中表现")]
        [SerializeField] private Color damageFlashColor = Color.white;
        [Min(0f)] [SerializeField] private float damageFlashDuration = 0.08f;

        [Header("死亡表现")]
        [SerializeField] private Color deathHighlightColor = new(1f, 0.75f, 0.2f, 1f);
        [Min(0f)] [SerializeField] private float deathHighlightDuration = 0.18f;

        [Inject] private IEffectService _effectService;

        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private Enemy _enemy;
        private EnemyFlockBridge _flockBridge;
        private Renderer[] _renderers;
        private float _flashTimer;
        private Color _activeFlashColor;
        private bool _subscribed;

        /// <summary>死亡高亮持续时间，供生成器决定回收时机。</summary>
        internal float DeathHighlightDuration => Mathf.Max(0f, deathHighlightDuration);

        /// <summary>缓存实体和其子级渲染器。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _flockBridge = GetComponent<EnemyFlockBridge>();
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>订阅实体伤害、死亡和复用事件。</summary>
        private void OnEnable()
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            if (_flockBridge == null)
            {
                _flockBridge = GetComponent<EnemyFlockBridge>();
            }

            if (_renderers == null)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }

            ClearFlash();
            EnsureSubscribed();
        }

        /// <summary>在宿主先于本组件完成激活时补做事件订阅。</summary>
        internal void EnsureSubscribed()
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            if (_flockBridge == null)
            {
                _flockBridge = GetComponent<EnemyFlockBridge>();
            }

            if (_renderers == null)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }

            if (_enemy != null && !_subscribed)
            {
                _enemy.DamageTaken += HandleDamageTaken;
                _enemy.DeathStarted += HandleDeathStarted;
                _enemy.ReuseRequested += HandleReuseRequested;
                _subscribed = true;
            }
        }

        /// <summary>注销事件并清除渲染器覆盖，避免对象池残留表现。</summary>
        private void OnDisable()
        {
            if (_enemy != null && _subscribed)
            {
                _enemy.DamageTaken -= HandleDamageTaken;
                _enemy.DeathStarted -= HandleDeathStarted;
                _enemy.ReuseRequested -= HandleReuseRequested;
                _subscribed = false;
            }

            ClearFlash();
        }

        /// <summary>按帧结束短时闪白或死亡高亮。</summary>
        private void Update()
        {
            if (_flashTimer <= 0f)
            {
                return;
            }

            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                ClearFlash();
            }
        }

        /// <summary>对象池租借时清除旧表现状态。</summary>
        public void OnRentFromPool()
        {
            ClearFlash();
        }

        /// <summary>对象池归还时清除表现状态。</summary>
        public void OnReturnToPool()
        {
            ClearFlash();
        }

        /// <summary>播放命中特效并应用短时闪白。</summary>
        /// <param name="enemy">发生命中的敌人。</param>
        /// <param name="damageAmount">实际扣除的伤害量。</param>
        private void HandleDamageTaken(Enemy enemy, float damageAmount)
        {
            if (_enemy != null && _enemy.Attributes != null)
            {
                _effectService?.Play(_enemy.Attributes.hitEffect, transform.position);
            }

            ApplyFlash(damageFlashColor, damageFlashDuration);
        }

        /// <summary>应用死亡高亮并播放死亡特效。</summary>
        /// <param name="enemy">进入死亡状态的敌人。</param>
        private void HandleDeathStarted(Enemy enemy)
        {
            ApplyFlash(deathHighlightColor, deathHighlightDuration);
            if (_enemy != null && _enemy.Attributes != null)
            {
                _effectService?.Play(_enemy.Attributes.deathEffect, transform.position);
            }
        }

        /// <summary>实体重置时清理表现状态。</summary>
        private void HandleReuseRequested()
        {
            ClearFlash();
        }

        /// <summary>设置渲染器颜色覆盖并同步旧调试观察字段。</summary>
        /// <param name="color">闪烁颜色。</param>
        /// <param name="duration">持续时间。</param>
        private void ApplyFlash(Color color, float duration)
        {
            _activeFlashColor = color;
            _flashTimer = Mathf.Max(0f, duration);
            _enemy?.SetDamagePresentationState(_activeFlashColor, _flashTimer);
            _flockBridge?.SetPresentationColor(_activeFlashColor);

            if (_renderers == null)
            {
                return;
            }

            for (int index = 0; index < _renderers.Length; index++)
            {
                Renderer renderer = _renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock block = new();
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorProperty, _activeFlashColor);
                block.SetColor(ColorProperty, _activeFlashColor);
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>移除渲染器颜色覆盖并同步旧调试观察字段。</summary>
        private void ClearFlash()
        {
            _flashTimer = 0f;
            _enemy?.ClearDamagePresentationState();
            _flockBridge?.ResetPresentationColor();

            if (_renderers == null)
            {
                return;
            }

            for (int index = 0; index < _renderers.Length; index++)
            {
                if (_renderers[index] != null)
                {
                    _renderers[index].SetPropertyBlock(null);
                }
            }
        }
    }
}
