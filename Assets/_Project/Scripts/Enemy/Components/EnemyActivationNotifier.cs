using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem.Components
{
    /// <summary>缓存敌人宿主上的激活监听器，避免通知时反复扫描组件。</summary>
    internal sealed class EnemyActivationNotifier
    {
        private readonly List<IEnemyActivationListener> _listeners;

        /// <summary>收集敌人对象当前挂载的激活监听器。</summary>
        /// <param name="enemy">提供监听器组件的敌人对象。</param>
        internal EnemyActivationNotifier(GameObject enemy)
        {
            MonoBehaviour[] components = enemy.GetComponents<MonoBehaviour>();
            _listeners = new List<IEnemyActivationListener>(components.Length);

            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is IEnemyActivationListener listener)
                {
                    _listeners.Add(listener);
                }
            }
        }

        /// <summary>通知缓存的敌人激活监听器。</summary>
        internal void Notify()
        {
            for (int index = 0; index < _listeners.Count; index++)
            {
                _listeners[index].OnEnemyActivationReady();
            }
        }
    }
}
