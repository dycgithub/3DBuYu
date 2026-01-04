using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// 泛型对象池 - 高效简洁的对象复用系统
    /// </summary>
    /// <typeparam name="T">可继承 MonoBehaviour 或实现 IPoolable 的类型</typeparam>
    public class ObjectPool<T> where T : Component
    {
        private readonly Stack<T> pool = new();
        private readonly T prefab;
        private readonly Transform parent;
        private readonly int prewarmCount;
        private readonly int maxSize;

        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="parent">对象父容器</param>
        /// <param name="prewarmCount">预实例化数量</param>
        /// <param name="maxSize">最大池大小 (0 = 无限制)</param>
        public ObjectPool(T prefab, Transform parent = null, int prewarmCount = 0, int maxSize = 0)
        {
            this.prefab = prefab;
            this.parent = parent;
            this.prewarmCount = prewarmCount;
            this.maxSize = maxSize > 0 ? maxSize : int.MaxValue;

            Prewarm();
        }

        /// <summary>
        /// 预实例化对象
        /// </summary>
        private void Prewarm()
        {
            for (int i = 0; i < prewarmCount; i++)
            {
                T obj = CreateNew();
                obj.gameObject.SetActive(false);
                pool.Push(obj);
            }
        }

        /// <summary>
        /// 创建新对象
        /// </summary>
        private T CreateNew()
        {
            GameObject go = Object.Instantiate(prefab.gameObject, parent);
            go.name = $"{prefab.name}_Pooled";
            return go.GetComponent<T>();
        }

        /// <summary>
        /// 从池中获取对象
        /// </summary>
        public T Get()
        {
            T obj = pool.Count > 0 ? pool.Pop() : CreateNew();
            obj.gameObject.SetActive(true);
            return obj;
        }

        /// <summary>
        /// 将对象释放回池中
        /// </summary>
        public void Release(T obj)
        {
            if (obj == null) return;

            obj.gameObject.SetActive(false);

            if (pool.Count < maxSize)
            {
                pool.Push(obj);
            }
            else
            {
                Object.Destroy(obj.gameObject);
            }
        }

        /// <summary>
        /// 释放所有对象到池中（用于清理）
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var obj in pool)
            {
                if (obj != null && obj.gameObject.activeInHierarchy)
                {
                    obj.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 清空池（销毁所有对象）
        /// </summary>
        public void Clear()
        {
            foreach (var obj in pool)
            {
                if (obj != null) Object.Destroy(obj.gameObject);
            }
            pool.Clear();
        }

        /// <summary>
        /// 当前池中对象数量
        /// </summary>
        public int Count => pool.Count;
    }

    /// <summary>
    /// 无需继承的对象池包装器（支持任意类型）
    /// </summary>
    public class ObjectPoolSimple<T> where T : class
    {
        private readonly Stack<T> pool = new();
        private readonly System.Func<T> createFunc;
        private readonly System.Action<T> onGet;
        private readonly System.Action<T> onRelease;
        private readonly int maxSize;

        public ObjectPoolSimple(
            System.Func<T> createFunc,
            System.Action<T> onGet = null,
            System.Action<T> onRelease = null,
            int prewarmCount = 0,
            int maxSize = 0)
        {
            this.createFunc = createFunc;
            this.onGet = onGet;
            this.onRelease = onRelease;
            this.maxSize = maxSize > 0 ? maxSize : int.MaxValue;

            for (int i = 0; i < prewarmCount; i++)
            {
                T obj = createFunc();
                pool.Push(obj);
            }
        }

        public T Get()
        {
            T obj = pool.Count > 0 ? pool.Pop() : createFunc();
            onGet?.Invoke(obj);
            return obj;
        }

        public void Release(T obj)
        {
            if (obj == null) return;
            onRelease?.Invoke(obj);
            if (pool.Count < maxSize) pool.Push(obj);
        }

        public int Count => pool.Count;

        public void Clear() => pool.Clear();
    }
}
