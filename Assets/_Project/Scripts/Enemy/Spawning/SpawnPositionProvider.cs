using System;
using System.Collections.Generic;
using Interfaces;
using UnityEngine;
using Services;
using VContainer;

namespace EnemySystem.Spawning
{
    /// <summary>
    /// 按配置的出生点和玩家安全距离计算敌人出生位置。
    /// </summary>
    public class SpawnPositionProvider : MonoBehaviour, ISpawnPositionProvider
    {
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
        [SerializeField] private Transform defaultSpawnPoint;
        [SerializeField] private float safeDistance = 10f;
        [SerializeField, Min(0f)] private float spawnJitterRadius = 0.5f;

        [Inject] private IPlayerCombatTarget _playerTarget;
        private int _lastSpawnPointIndex = -1;

        /// <summary>
        /// 获取远离玩家的出生点，并为出生点添加确定性的轻微扰动。
        /// </summary>
        /// <returns>敌人的世界出生位置。</returns>
        public Vector3 GetSpawnPosition()
        {
            if (_playerTarget == null || _playerTarget.Transform == null)
            {
                throw new InvalidOperationException(
                    "[SpawnPositionProvider] 未注入 IPlayerCombatTarget，无法计算安全出生点。");
            }

            Transform playerTransform = _playerTarget.Transform;
            if (spawnPoints != null && spawnPoints.Count > 0)
            {
                int startIndex = UnityEngine.Random.Range(0, spawnPoints.Count);
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
                    if (Vector3.Distance(candidate, playerTransform.position) < safeDistance)
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

            throw new InvalidOperationException(
                "[SpawnPositionProvider] 未配置有效 spawnPoints 或 defaultSpawnPoint。");
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
