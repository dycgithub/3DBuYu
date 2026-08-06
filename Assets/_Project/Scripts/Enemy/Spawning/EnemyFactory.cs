using UnityEngine;
using FlockingSystem;
using Services;
using VContainer;

namespace EnemySystem.Spawning
{
    public class EnemyFactory
    {
        private readonly EnemyPool _pool;
        private readonly FlockManager _flockManager;

        [Inject]
        public EnemyFactory(EnemyPool pool, FlockManager flockManager)
        {
            _pool = pool;
            _flockManager = flockManager;
        }

        public Enemy Create(GameObject prefab, UnityEngine.Vector3 position, WaveData currentWave)
        {
            var obj = _pool.Get(prefab);
            obj.transform.SetPositionAndRotation(position, UnityEngine.Quaternion.identity);

            var enemy = obj.GetComponent<Enemy>();
            enemy.SourcePrefab = prefab;
            enemy.ResetForReuse();
            enemy.ApplyStats(currentWave.healthMultiplier, currentWave.speedMultiplier);

            var flockAgent = enemy.GetComponent<FlockAgent>();
            if (flockAgent != null)
                flockAgent.Initialize(_flockManager);

            obj.SetActive(true);
            return enemy;
        }

        public void Release(Enemy enemy)
        {
            if (enemy == null) return;
            var flockAgent = enemy.GetComponent<FlockAgent>();
            if (flockAgent != null)
                _flockManager.Unregister(flockAgent);
            _pool.Release(enemy.gameObject);
        }
    }
}