using System.Collections.Generic;
using Services;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 母体敌人的周期子敌人生成行为。
    /// 子敌人独立存活，但只由母体负责生成数量上限。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyMotherComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        [Inject] private IEnemyChildSpawner _childSpawner;

        private readonly HashSet<Enemy> _children = new();
        private Enemy _enemy;
        private float _spawnTimer;

        /// <summary>当前母体生成的存活子敌人数量。</summary>
        public int ActiveChildCount => _children.Count;

        /// <summary>缓存宿主。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        /// <summary>启用时立即允许第一次生成。</summary>
        private void OnEnable()
        {
            _spawnTimer = 0f;
        }

        /// <summary>销毁时解除仍存活子敌人的事件订阅。</summary>
        private void OnDestroy()
        {
            ClearChildren();
        }

        /// <summary>按生成间隔创建一个子敌人。</summary>
        private void Update()
        {
            if (_enemy == null || _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            RemoveDeadChildren();
            EnemyAttributesSO attributes = _enemy.Attributes;
            if (_childSpawner == null ||
                attributes.childPrefab == null ||
                _children.Count >= Mathf.Max(0, attributes.childMaximumAlive))
            {
                return;
            }

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f)
            {
                return;
            }

            _spawnTimer = Mathf.Max(0.01f, attributes.childSpawnInterval);
            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, attributes.childSpawnRadius);
            Vector3 spawnPosition = transform.position + new Vector3(offset.x, 0f, offset.y);
            Enemy child = _childSpawner.TrySpawnChild(
                _enemy,
                attributes.childPrefab,
                spawnPosition,
                attributes.childHealthMultiplier,
                attributes.childSpeedMultiplier);
            if (child == null)
            {
                return;
            }

            _children.Add(child);
            child.OnDied += HandleChildDied;
        }

        /// <summary>处理子敌人死亡并释放母体计数。</summary>
        private void HandleChildDied(Enemy child)
        {
            if (child == null)
            {
                return;
            }

            child.OnDied -= HandleChildDied;
            _children.Remove(child);
        }

        /// <summary>移除已死亡或已禁用的子敌人。</summary>
        private void RemoveDeadChildren()
        {
            _deadChildren.Clear();
            foreach (Enemy child in _children)
            {
                if (child == null || child.IsDead || !child.gameObject.activeInHierarchy)
                {
                    _deadChildren.Add(child);
                }
            }

            foreach (Enemy child in _deadChildren)
            {
                if (child != null)
                {
                    child.OnDied -= HandleChildDied;
                }

                _children.Remove(child);
            }
        }

        private readonly List<Enemy> _deadChildren = new();

        /// <summary>清除全部子敌人事件订阅和本地计数。</summary>
        private void ClearChildren()
        {
            foreach (Enemy child in _children)
            {
                if (child != null)
                {
                    child.OnDied -= HandleChildDied;
                }
            }

            _children.Clear();
            _deadChildren.Clear();
        }

        /// <summary>对象池租借时清除生成计时，保留仍存活子敌人的归属记录。</summary>
        public void OnRentFromPool()
        {
            _spawnTimer = 0f;
        }

        /// <summary>对象池归还时清除生成计时，保留仍存活子敌人的归属记录。</summary>
        public void OnReturnToPool()
        {
            _spawnTimer = 0f;
        }

        /// <summary>补做宿主激活后的依赖缓存。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            _enemy ??= GetComponent<Enemy>();
        }
    }
}
