using System.Collections.Generic;
using UnityEngine;
using Services;

namespace EnemySystem.Spawning
{
    /// <summary>
    /// 按配置的出生点和玩家安全距离计算敌人出生位置。
    /// </summary>
    public class SpawnPositionProvider : MonoBehaviour, ISpawnPositionProvider
    {
        private const float FallbackSpawnRadius = 10f;

        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
        [SerializeField] private Transform defaultSpawnPoint;
        [SerializeField] private float safeDistance = 10f;
        [SerializeField, Min(0f)] private float spawnJitterRadius = 0.5f;

        private Transform _playerTransform;
        private int _lastSpawnPointIndex = -1;

        /// <summary>
        /// 在场景启动时缓存玩家变换。
        /// </summary>
        private void Start()
        {
            CachePlayerTransform();
        }

        /// <summary>
        /// 按 Player 标签查找并缓存玩家变换。
        /// </summary>
        private void CachePlayerTransform()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                _playerTransform = playerObject.transform;
            }
        }

        /// <summary>
        /// 获取远离玩家的出生点，并为出生点添加确定性的轻微扰动。
        /// </summary>
        /// <returns>敌人的世界出生位置。</returns>
        public Vector3 GetSpawnPosition()
        {
            if (_playerTransform == null)
            {
                CachePlayerTransform();
            }

            if (spawnPoints.Count > 0)
            {
                int startIndex = Random.Range(0, spawnPoints.Count);
                for (int offset = 0; offset < spawnPoints.Count; offset++)
                {
                    int index = (startIndex + offset) % spawnPoints.Count;
                    if (spawnPoints.Count > 1 && index == _lastSpawnPointIndex)
                    {
                        continue;
                    }

                    Transform point = spawnPoints[index];
                    if (point == null)
                    {
                        continue;
                    }

                    Vector3 candidate = point.position + CalculateSpawnOffset(index);
                    if (_playerTransform != null
                        && Vector3.Distance(candidate, _playerTransform.position) < safeDistance)
                    {
                        continue;
                    }

                    _lastSpawnPointIndex = index;
                    return candidate;
                }
            }

            if (defaultSpawnPoint != null)
            {
                return defaultSpawnPoint.position;
            }

            return transform.position + Random.insideUnitSphere * FallbackSpawnRadius;
        }

        /// <summary>
        /// 根据出生点索引计算低成本的确定性扰动。
        /// </summary>
        /// <param name="index">出生点索引。</param>
        /// <returns>出生点位置偏移。</returns>
        private Vector3 CalculateSpawnOffset(int index)
        {
            if (spawnJitterRadius <= 0f || spawnPoints.Count <= 1)
            {
                return Vector3.zero;
            }

            float angle = index * 2.399963f;
            return new Vector3(
                Mathf.Cos(angle),
                0f,
                Mathf.Sin(angle)) * spawnJitterRadius;
        }
    }
}
