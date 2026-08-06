using System.Collections.Generic;
using System.Linq;
using R3;
using Services;

namespace EnemySystem.Spawning
{
    public class EnemySpawner : IEnemySpawner
    {
        private readonly EnemyFactory _factory;
        private readonly ISpawnPositionProvider _spawnPositionProvider;
        private readonly HashSet<Enemy> _activeEnemies = new();
        private readonly ReactiveProperty<int> _activeEnemyCount = new(0);

        public int ActiveEnemyCount => _activeEnemyCount.Value;
        public Observable<int> OnActiveEnemyCountChanged => _activeEnemyCount;

        public EnemySpawner(EnemyFactory factory, ISpawnPositionProvider spawnPositionProvider)
        {
            _factory = factory;
            _spawnPositionProvider = spawnPositionProvider;
        }

        public void SpawnEnemy(WaveEnemyInfo info, WaveData currentWave)
        {
            if (info == null || info.prefab == null) return;

            var pos = _spawnPositionProvider.GetSpawnPosition();
            var enemy = _factory.Create(info.prefab.gameObject, pos, currentWave);
            enemy.OnDied += HandleEnemyDied;
            _activeEnemies.Add(enemy);
            _activeEnemyCount.Value = _activeEnemies.Count;
        }

        private void HandleEnemyDied(Enemy enemy)
        {
            enemy.OnDied -= HandleEnemyDied;
            _activeEnemies.Remove(enemy);
            _factory.Release(enemy);
            _activeEnemyCount.Value = _activeEnemies.Count;
        }

        public void ClearAll()
        {
            foreach (var enemy in _activeEnemies.ToArray())
                _factory.Release(enemy);
            _activeEnemies.Clear();
            _activeEnemyCount.Value = 0;
        }

        public void KillAllEnemies()
        {
            foreach (var enemy in _activeEnemies.ToArray())
                enemy.Kill();
        }
    }
}