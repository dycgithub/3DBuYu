using System.Collections.Generic;
using UnityEngine;

namespace ShootingSystem
{
    public class ProjectileVisualPool
    {
        private class PoolPerProfile
        {
            public Queue<GameObject> Available = new Queue<GameObject>();
            public List<GameObject> Active = new List<GameObject>();
            public GameObject Prefab;
        }

        private class SlotInfo
        {
            public PoolPerProfile Pool;
            public int Index;
        }

        private Dictionary<int, PoolPerProfile> _pools = new Dictionary<int, PoolPerProfile>();
        private Dictionary<int, SlotInfo> _slotMap = new Dictionary<int, SlotInfo>();
        private int _nextSlot = 0;

        public int Allocate(BulletProfile profile, Vector3 pos, Quaternion rot)
        {
            Debug.Log("从ProjectileVisualPool分配子弹");
            if (profile == null || profile.Visual == null || profile.Visual.Prefab == null) return -1;
            int id = profile.GetInstanceID();
            if (!_pools.TryGetValue(id, out var pool))
            {
                pool = new PoolPerProfile { Prefab = profile.Visual.Prefab };
                _pools[id] = pool;
            }

            GameObject go;
            if (pool.Available.Count > 0)
            {
                go = pool.Available.Dequeue();
                go.transform.SetPositionAndRotation(pos, rot);
                go.SetActive(true);
            }
            else
            {
                go = Object.Instantiate(pool.Prefab, pos, rot);
            }

            int index = pool.Active.Count;
            pool.Active.Add(go);

            int slot = _nextSlot++;
            _slotMap[slot] = new SlotInfo { Pool = pool, Index = index };
            return slot;
        }

        public void UpdateTransform(int slot, Vector3 pos, Quaternion rot)
        {
            if (slot < 0) return;
            if (_slotMap.TryGetValue(slot, out var info))
            {
                var go = info.Pool.Active[info.Index];
                if (go != null)
                    go.transform.SetPositionAndRotation(pos, rot);
            }
        }

        public void Release(int slot)
        {
            if (slot < 0) return;
            if (!_slotMap.TryGetValue(slot, out var info)) return;

            var go = info.Pool.Active[info.Index];
            if (go != null)
            {
                go.SetActive(false);
                info.Pool.Available.Enqueue(go);
            }

            info.Pool.Active[info.Index] = null;
            _slotMap.Remove(slot);
        }
    }
}
