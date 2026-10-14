using System;
using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using EnemySystem.Components;
using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;
using Utils;
using VContainer;
using VContainer.Unity;

namespace _Project.UI.EnemyThreatMarker
{
    /// <summary>用空间网格识别近身接触威胁，并集中租借屏幕标记图片。</summary>
    public sealed class EnemyThreatMarkerSystem : ITickable, ILateTickable, IDisposable
    {
        private readonly IPlayerCombatTarget _playerTarget;
        private readonly ISpatialQueryService _spatialQueryService;
        private readonly ICombatPhaseService _combatPhase;
        private readonly IGameObjectPool _pool;
        private readonly IObjectResolver _resolver;
        private readonly EnemyThreatMarkerLayer _layer;
        private readonly Func<GameObject, GameObject> _imageFactory;
        private readonly Dictionary<Enemy, UnityEngine.UI.Image> _markers = new();
        private readonly List<Enemy> _releaseBuffer = new();
        private readonly float _enterDistance;
        private readonly float _enterDistanceSquared;
        private readonly float _exitDistanceSquared;
        private GameObject _poolPrefab;
        private bool _disposed;
        private bool _warnedMissingImage;

        /// <summary>注入玩家、空间查询、战斗阶段和场景标记层。</summary>
        public EnemyThreatMarkerSystem(
            IPlayerCombatTarget playerTarget,
            ISpatialQueryService spatialQueryService,
            ICombatPhaseService combatPhase,
            IGameObjectPool pool,
            IObjectResolver resolver,
            EnemyThreatMarkerLayer layer)
        {
            _playerTarget = playerTarget;
            _spatialQueryService = spatialQueryService;
            _combatPhase = combatPhase;
            _pool = pool;
            _resolver = resolver;
            _layer = layer;
            _imageFactory = InstantiateImage;
            _enterDistance = layer.EnterDistance;
            _enterDistanceSquared = _enterDistance * _enterDistance;
            float exitDistance = layer.ExitDistance;
            _exitDistanceSquared = exitDistance * exitDistance;
        }

        /// <summary>查询新进入范围的敌人并清理已离开或失效的标记。</summary>
        public void Tick()
        {
            if (_disposed)
            {
                return;
            }

            if (!CanTrackThreats())
            {
                ReleaseAll();
                return;
            }

            Vector3 playerPosition = _playerTarget.Transform.position;
            AddNearbyEnemies(playerPosition);
            RemoveDistantEnemies(playerPosition);
        }

        /// <summary>在帧末投影标记，跟随 ECS 已提交的敌人位置。</summary>
        public void LateTick()
        {
            if (_disposed)
            {
                return;
            }

            foreach (KeyValuePair<Enemy, UnityEngine.UI.Image> marker in _markers)
            {
                if (!CanMark(marker.Key))
                {
                    marker.Value.enabled = false;
                    continue;
                }

                _layer.SetImagePosition(marker.Value, marker.Key.transform.position);
            }
        }

        /// <summary>归还活动图片并清理系统独占的对象池 Bucket。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ReleaseAll();
            if (_poolPrefab != null)
            {
                _pool.Clear(_poolPrefab);
                _poolPrefab = null;
            }

            _disposed = true;
        }

        /// <summary>验证玩家和战斗阶段允许显示接触威胁提示。</summary>
        private bool CanTrackThreats()
        {
            return _combatPhase != null &&
                _combatPhase.CanPerformCombatActions &&
                _playerTarget != null &&
                _playerTarget.IsAlive &&
                _playerTarget.Transform != null;
        }

        /// <summary>从空间网格取得进入半径内且能造成接触伤害的敌人。</summary>
        private void AddNearbyEnemies(Vector3 playerPosition)
        {
            List<IDamageable> candidates = _spatialQueryService.QueryRadius(
                playerPosition,
                _enterDistance,
                SpatialLayers.Enemy);

            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index] is not Enemy enemy ||
                    _markers.ContainsKey(enemy) ||
                    !CanMark(enemy))
                {
                    continue;
                }

                Vector3 offset = enemy.transform.position - playerPosition;
                if (EnemyThreatRange.ShouldMark(
                        offset.sqrMagnitude,
                        false,
                        _enterDistanceSquared,
                        _exitDistanceSquared))
                {
                    RentMarker(enemy);
                }
            }
        }

        /// <summary>清理离开退出半径、死亡或失去接触伤害能力的敌人。</summary>
        private void RemoveDistantEnemies(Vector3 playerPosition)
        {
            _releaseBuffer.Clear();
            foreach (KeyValuePair<Enemy, UnityEngine.UI.Image> marker in _markers)
            {
                Enemy enemy = marker.Key;
                if (!CanMark(enemy))
                {
                    _releaseBuffer.Add(enemy);
                    continue;
                }

                Vector3 offset = enemy.transform.position - playerPosition;
                if (!EnemyThreatRange.ShouldMark(
                        offset.sqrMagnitude,
                        true,
                        _enterDistanceSquared,
                        _exitDistanceSquared))
                {
                    _releaseBuffer.Add(enemy);
                }
            }

            for (int index = 0; index < _releaseBuffer.Count; index++)
            {
                ReleaseMarker(_releaseBuffer[index]);
            }

            _releaseBuffer.Clear();
        }

        /// <summary>判断敌人仍存活且接触伤害组件可用。</summary>
        private static bool CanMark(Enemy enemy)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDead)
            {
                return false;
            }

            EnemyContactDamageComponent contactDamage =
                enemy.GetComponent<EnemyContactDamageComponent>();
            return contactDamage != null &&
                contactDamage.isActiveAndEnabled &&
                enemy.GetAttackAllowedMultiplier() > 0f &&
                enemy.GetAttackDamageMultiplier() > 0f;
        }

        /// <summary>租借一张标记图片并关联到指定敌人。</summary>
        private void RentMarker(Enemy enemy)
        {
            if (!TryGetImagePrefab(out GameObject prefab))
            {
                return;
            }

            _poolPrefab = prefab;
            GameObject instance = _pool.Rent(
                prefab,
                new PoolSettings(0, _layer.MaximumRetainedImages),
                _layer.Root,
                true,
                _imageFactory);
            if (instance == null)
            {
                return;
            }

            UnityEngine.UI.Image image = instance.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
            {
                _pool.Return(instance);
                return;
            }

            image.enabled = false;
            _markers.Add(enemy, image);
        }

        /// <summary>读取并校验可替换的标记 Image 预制体。</summary>
        private bool TryGetImagePrefab(out GameObject prefab)
        {
            prefab = _layer != null ? _layer.ImagePrefab : null;
            if (prefab != null && prefab.GetComponent<UnityEngine.UI.Image>() != null)
            {
                return true;
            }

            if (!_warnedMissingImage)
            {
                Debug.LogWarning("[EnemyThreatMarkerSystem] 未配置带 Image 的威胁标记预制体。");
                _warnedMissingImage = true;
            }

            return false;
        }

        /// <summary>归还敌人对应的图片并移除标记映射。</summary>
        private void ReleaseMarker(Enemy enemy)
        {
            if (!_markers.Remove(enemy, out UnityEngine.UI.Image image) || image == null)
            {
                return;
            }

            image.enabled = false;
            _pool.Return(image.gameObject);
        }

        /// <summary>归还当前所有活动标记。</summary>
        private void ReleaseAll()
        {
            _releaseBuffer.Clear();
            foreach (Enemy enemy in _markers.Keys)
            {
                _releaseBuffer.Add(enemy);
            }

            for (int index = 0; index < _releaseBuffer.Count; index++)
            {
                ReleaseMarker(_releaseBuffer[index]);
            }

            _releaseBuffer.Clear();
        }

        /// <summary>由当前场景容器实例化图片预制体并注入组件依赖。</summary>
        private GameObject InstantiateImage(GameObject prefab)
        {
            return _resolver.Instantiate(prefab);
        }
    }
}
