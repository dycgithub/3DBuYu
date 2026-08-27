using System.Collections.Generic;
using R3;
using Services;
using VContainer.Unity;

namespace EnemySystem.Spawning
{
    /// <summary>管理敌人生成、死亡通知和延迟对象池回收。</summary>
    public class EnemySpawner : IEnemySpawner, ITickable
    {
        private readonly EnemyFactory _factory;
        private readonly ISpawnPositionProvider _spawnPositionProvider;
        private readonly HashSet<Enemy> _activeEnemies = new();
        private readonly HashSet<Enemy> _pendingRelease = new();
        private readonly List<Enemy> _iterationBuffer = new(128);
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
            // 延迟到本帧战斗效果派发结束后再回收到对象池。
            _pendingRelease.Add(enemy);
            _activeEnemyCount.Value = _activeEnemies.Count;
        }

        public void Tick()
        {
            if (_pendingRelease.Count == 0)
                return;

            foreach (Enemy enemy in _pendingRelease)
                _factory.Release(enemy);

            _pendingRelease.Clear();
        }

        public void ClearAll()
        {
            Tick();
            CopyActiveEnemies();
            for (int index = 0; index < _iterationBuffer.Count; index++)
            {
                Enemy enemy = _iterationBuffer[index];
                enemy.OnDied -= HandleEnemyDied;
                _factory.Release(enemy);
            }

            _iterationBuffer.Clear();
            _activeEnemies.Clear();
            _activeEnemyCount.Value = 0;
        }

        public void KillAllEnemies()
        {
            CopyActiveEnemies();
            for (int index = 0; index < _iterationBuffer.Count; index++)
                _iterationBuffer[index].Kill();

            _iterationBuffer.Clear();
        }

        private void CopyActiveEnemies()
        {
            _iterationBuffer.Clear();
            foreach (Enemy enemy in _activeEnemies)
                _iterationBuffer.Add(enemy);
        }
    }
}
