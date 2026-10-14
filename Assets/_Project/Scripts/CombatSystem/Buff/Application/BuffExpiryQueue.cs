using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>以可更新最小堆维护活动 Buff 的绝对到期时刻。</summary>
    internal sealed class BuffExpiryQueue
    {
        private readonly List<BuffExpiryNode> _nodes = new();

        /// <summary>插入尚未排队的有限时实例。</summary>
        internal void Add(BuffInstance instance)
        {
            if (instance == null || instance.ExpiryQueueIndex >= 0 ||
                float.IsPositiveInfinity(instance.ExpireAt))
            {
                return;
            }

            instance.ExpiryQueueIndex = _nodes.Count;
            _nodes.Add(new BuffExpiryNode(instance, instance.ExpireAt));
            SiftUp(instance.ExpiryQueueIndex);
        }

        /// <summary>更新实例到期时刻，索引无效时重新插入。</summary>
        internal void Update(BuffInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.ExpiryQueueIndex < 0)
            {
                Add(instance);
                return;
            }

            int index = instance.ExpiryQueueIndex;
            if (index >= _nodes.Count || !ReferenceEquals(_nodes[index].Instance, instance))
            {
                instance.ExpiryQueueIndex = -1;
                Add(instance);
                return;
            }

            BuffExpiryNode node = _nodes[index];
            node.DueAt = instance.ExpireAt;
            _nodes[index] = node;
            SiftUp(index);
            SiftDown(instance.ExpiryQueueIndex);
        }

        /// <summary>按实例移除到期节点。</summary>
        internal void Remove(BuffInstance instance)
        {
            if (instance == null || instance.ExpiryQueueIndex < 0)
            {
                return;
            }

            int index = instance.ExpiryQueueIndex;
            instance.ExpiryQueueIndex = -1;
            if (index >= _nodes.Count || !ReferenceEquals(_nodes[index].Instance, instance))
            {
                return;
            }

            int lastIndex = _nodes.Count - 1;
            if (index == lastIndex)
            {
                _nodes.RemoveAt(lastIndex);
                return;
            }

            BuffExpiryNode replacement = _nodes[lastIndex];
            _nodes[index] = replacement;
            replacement.Instance.ExpiryQueueIndex = index;
            _nodes.RemoveAt(lastIndex);
            SiftUp(index);
            SiftDown(replacement.Instance.ExpiryQueueIndex);
        }

        /// <summary>读取当前最早到期节点，不移除堆顶。</summary>
        internal bool TryPeek(out BuffExpiryNode node)
        {
            if (_nodes.Count == 0)
            {
                node = default;
                return false;
            }

            node = _nodes[0];
            return true;
        }

        /// <summary>清空到期堆。</summary>
        internal void Clear()
        {
            _nodes.Clear();
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!IsEarlier(_nodes[index], _nodes[parent]))
                {
                    break;
                }

                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= _nodes.Count)
                {
                    return;
                }

                int right = left + 1;
                int smallest = left;
                if (right < _nodes.Count && IsEarlier(_nodes[right], _nodes[left]))
                {
                    smallest = right;
                }

                if (!IsEarlier(_nodes[smallest], _nodes[index]))
                {
                    return;
                }

                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int first, int second)
        {
            BuffExpiryNode value = _nodes[first];
            _nodes[first] = _nodes[second];
            _nodes[second] = value;
            _nodes[first].Instance.ExpiryQueueIndex = first;
            _nodes[second].Instance.ExpiryQueueIndex = second;
        }

        private static bool IsEarlier(BuffExpiryNode left, BuffExpiryNode right)
        {
            if (left.DueAt < right.DueAt)
            {
                return true;
            }

            return left.DueAt == right.DueAt &&
                left.Instance.InstanceId < right.Instance.InstanceId;
        }
    }

    /// <summary>保存到期堆中的实例与其绝对到期时刻。</summary>
    internal struct BuffExpiryNode
    {
        internal BuffInstance Instance;
        internal float DueAt;

        internal BuffExpiryNode(BuffInstance instance, float dueAt)
        {
            Instance = instance;
            DueAt = dueAt;
        }
    }
}
