using System.Collections.Generic;
using UnityEngine;
using Services;

namespace EnemySystem.Spawning
{
    public class SpawnPositionProvider : MonoBehaviour, ISpawnPositionProvider
    {
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
        [SerializeField] private Transform defaultSpawnPoint;
        [SerializeField] private float safeDistance = 10f;

        private Transform _playerTransform;

        private void Start()
        {
            ResolvePlayer();
        }

        private void ResolvePlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        public Vector3 GetSpawnPosition()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (spawnPoints.Count == 0) break;
                var p = spawnPoints[Random.Range(0, spawnPoints.Count)];
                if (p == null) continue;
                if (_playerTransform != null && Vector3.Distance(p.position, _playerTransform.position) < safeDistance)
                    continue;
                return p.position;
            }

            if (defaultSpawnPoint != null) return defaultSpawnPoint.position;
            return transform.position + Random.insideUnitSphere * 10f;
        }
    }
}