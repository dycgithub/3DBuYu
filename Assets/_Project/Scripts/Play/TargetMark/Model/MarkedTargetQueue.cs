using System;
using System.Collections.Generic;
using Interfaces;

namespace Play.TargetMark
{
    /// <summary>保存有效标记目标并维护稳定的先进先出顺序。</summary>
    public sealed class MarkedTargetQueue
    {
        private readonly List<IDamageable> _targets = new();
        private readonly IReadOnlyList<IDamageable> _readOnlyTargets;

        /// <summary>创建只读视图和空目标队列。</summary>
        public MarkedTargetQueue()
        {
            _readOnlyTargets = _targets.AsReadOnly();
        }

        /// <summary>按追加顺序读取队列中的目标。</summary>
        public IReadOnlyList<IDamageable> Targets => _readOnlyTargets;

        /// <summary>读取当前队首目标；队列为空时返回 null。</summary>
        public IDamageable CurrentTarget => _targets.Count > 0 ? _targets[0] : null;

        /// <summary>队列内容发生变化时触发。</summary>
        public event Action Changed;

        /// <summary>追加有效且尚未入队的目标。</summary>
        /// <param name="target">待追加的可受伤害目标。</param>
        /// <returns>目标成功追加时返回 true。</returns>
        public bool TryAppend(IDamageable target)
        {
            if (!target.IsAliveAndValid() || Contains(target))
            {
                return false;
            }

            _targets.Add(target);
            Changed?.Invoke();
            return true;
        }

        /// <summary>移除指定目标并保留其他目标的相对顺序。</summary>
        /// <param name="target">待移除的目标。</param>
        /// <returns>目标原本位于队列中时返回 true。</returns>
        public bool Remove(IDamageable target)
        {
            int index = FindTarget(target);
            if (index < 0)
            {
                return false;
            }

            _targets.RemoveAt(index);
            Changed?.Invoke();
            return true;
        }

        /// <summary>批量移除不满足条件的目标，并仅发布一次队列变更。</summary>
        /// <param name="isValid">判断目标是否仍应保留的条件。</param>
        /// <returns>本次移除的目标数量。</returns>
        public int RemoveInvalidTargets(Predicate<IDamageable> isValid)
        {
            int removedCount = 0;
            for (int index = _targets.Count - 1; index >= 0; index--)
            {
                if (isValid(_targets[index]))
                {
                    continue;
                }

                _targets.RemoveAt(index);
                removedCount++;
            }

            if (removedCount > 0)
            {
                Changed?.Invoke();
            }

            return removedCount;
        }

        /// <summary>清空队列；仅在队列非空时通知变化。</summary>
        public void Clear()
        {
            if (_targets.Count == 0)
            {
                return;
            }

            _targets.Clear();
            Changed?.Invoke();
        }

        /// <summary>判断目标是否已按对象引用入队。</summary>
        private bool Contains(IDamageable target)
        {
            return FindTarget(target) >= 0;
        }

        /// <summary>按对象引用查找目标所在位置。</summary>
        private int FindTarget(IDamageable target)
        {
            for (int index = 0; index < _targets.Count; index++)
            {
                if (ReferenceEquals(_targets[index], target))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
