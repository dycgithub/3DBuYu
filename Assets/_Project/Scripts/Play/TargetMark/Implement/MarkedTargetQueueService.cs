using System;
using System.Collections.Generic;
using EnemySystem;
using GameSystem;
using Interfaces;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Play.TargetMark
{
    /// <summary>编排标记目标队列、活动队首和场景生命周期。</summary>
    public sealed class MarkedTargetQueueService : ITickable, IDisposable
    {
        private readonly MarkedTargetQueue _queue = new();
        private readonly MarkedTargetService _markedTarget;
        private readonly GameManager _gameManager;
        private readonly Dictionary<Enemy, Action> _trackedEnemies = new();
        private readonly HashSet<Enemy> _queuedEnemies = new();
        private readonly List<Enemy> _pendingEnemyUnsubscriptions = new();

        /// <summary>注入活动标记状态和本局结算事件。</summary>
        [Inject]
        public MarkedTargetQueueService(
            MarkedTargetService markedTarget,
            GameManager gameManager)
        {
            _markedTarget = markedTarget;
            _gameManager = gameManager;
            _queue.Changed += HandleQueueChanged;
            _gameManager.OnSettled += HandleSettled;
        }

        /// <summary>按追加顺序读取队列中的目标。</summary>
        public IReadOnlyList<IDamageable> Targets => _queue.Targets;

        /// <summary>读取当前队首目标；队列为空时返回 null。</summary>
        public IDamageable CurrentTarget => _queue.CurrentTarget;

        /// <summary>队列内容发生变化时触发。</summary>
        public event Action TargetsChanged;

        /// <summary>追加有效且尚未入队的目标。</summary>
        /// <param name="target">待追加的可受伤害目标。</param>
        /// <returns>目标成功追加时返回 true。</returns>
        public bool TryAppend(IDamageable target)
        {
            return _queue.TryAppend(target);
        }

        /// <summary>从队列移除指定目标。</summary>
        /// <param name="target">待移除的目标。</param>
        /// <returns>目标原本位于队列中时返回 true。</returns>
        public bool Remove(IDamageable target)
        {
            return _queue.Remove(target);
        }

        /// <summary>清空队列并同步清除活动标记。</summary>
        public void Clear()
        {
            _queue.Clear();
            _markedTarget.ClearTarget();
        }

        /// <summary>批量移除失活或无效目标，并同步队首状态。</summary>
        public void Tick()
        {
            _queue.RemoveInvalidTargets(IsTargetActive);
        }

        /// <summary>清空状态并退订场景和敌人事件。</summary>
        public void Dispose()
        {
            Clear();
            _gameManager.OnSettled -= HandleSettled;
            _queue.Changed -= HandleQueueChanged;
            UnsubscribeEnemyLifecycle();
        }

        /// <summary>在队列变化后更新死亡监听和活动队首。</summary>
        private void HandleQueueChanged()
        {
            SyncEnemyLifecycleEvents();
            SyncMarkedTarget();
            TargetsChanged?.Invoke();
        }

        /// <summary>将队首同步到现有活动标记状态。</summary>
        private void SyncMarkedTarget()
        {
            IDamageable target = _queue.CurrentTarget;
            if (!IsTargetActive(target))
            {
                _markedTarget.ClearTarget();
                return;
            }

            _markedTarget.TrySetTarget(target);
        }

        /// <summary>使敌人死亡和对象池复用订阅与当前队列保持一致。</summary>
        private void SyncEnemyLifecycleEvents()
        {
            _queuedEnemies.Clear();
            for (int index = 0; index < _queue.Targets.Count; index++)
            {
                if (_queue.Targets[index] is Enemy enemy)
                {
                    _queuedEnemies.Add(enemy);
                }
            }

            _pendingEnemyUnsubscriptions.Clear();
            foreach (Enemy enemy in _trackedEnemies.Keys)
            {
                if (!_queuedEnemies.Contains(enemy))
                {
                    _pendingEnemyUnsubscriptions.Add(enemy);
                }
            }

            for (int index = 0; index < _pendingEnemyUnsubscriptions.Count; index++)
            {
                Enemy enemy = _pendingEnemyUnsubscriptions[index];
                if (enemy != null)
                {
                    enemy.OnDied -= HandleEnemyDied;
                    enemy.ReuseRequested -= _trackedEnemies[enemy];
                }

                _trackedEnemies.Remove(enemy);
            }

            _pendingEnemyUnsubscriptions.Clear();
            foreach (Enemy enemy in _queuedEnemies)
            {
                if (_trackedEnemies.ContainsKey(enemy))
                {
                    continue;
                }

                Action reuseHandler = () => HandleEnemyReused(enemy);
                _trackedEnemies.Add(enemy, reuseHandler);
                enemy.OnDied += HandleEnemyDied;
                enemy.ReuseRequested += reuseHandler;
            }
        }

        /// <summary>解除当前所有敌人的生命周期事件订阅。</summary>
        private void UnsubscribeEnemyLifecycle()
        {
            foreach (KeyValuePair<Enemy, Action> trackedEnemy in _trackedEnemies)
            {
                Enemy enemy = trackedEnemy.Key;
                if (enemy != null)
                {
                    enemy.OnDied -= HandleEnemyDied;
                    enemy.ReuseRequested -= trackedEnemy.Value;
                }
            }

            _trackedEnemies.Clear();
            _queuedEnemies.Clear();
            _pendingEnemyUnsubscriptions.Clear();
        }

        /// <summary>敌人死亡时立即移除其队列引用。</summary>
        /// <param name="enemy">死亡的敌人实体。</param>
        private void HandleEnemyDied(Enemy enemy)
        {
            _queue.Remove(enemy);
        }

        /// <summary>敌人被对象池归还或重新租借时立即移除旧标记。</summary>
        /// <param name="enemy">状态刚被重置的敌人实体。</param>
        private void HandleEnemyReused(Enemy enemy)
        {
            _queue.Remove(enemy);
        }

        /// <summary>胜利或失败结算时清除本局目标。</summary>
        /// <param name="victory">本局是否胜利。</param>
        /// <param name="points">结算时的本局积分。</param>
        private void HandleSettled(bool victory, int points)
        {
            Clear();
        }

        /// <summary>检查目标存活有效且其 Unity 组件处于启用状态。</summary>
        private static bool IsTargetActive(IDamageable target)
        {
            return target.IsAliveAndValid() &&
                (target is not Behaviour behaviour || behaviour.isActiveAndEnabled);
        }
    }
}
