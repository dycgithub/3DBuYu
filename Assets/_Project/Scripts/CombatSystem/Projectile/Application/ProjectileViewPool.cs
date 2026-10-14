using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace CombatSystem
{
    /// <summary>
    /// 管理投射物与激光 Beam 的视觉租借、颜色应用和对象池回收。
    /// </summary>
    public sealed class ProjectileViewPool : IDisposable
    {
        private const int DefaultMaximumRetainedViewCount = 256;
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        private readonly IGameObjectPool _pool;
        private readonly HashSet<GameObject> _prefabs = new();
        private readonly List<Renderer> _renderers = new();
        private readonly MaterialPropertyBlock _tintProperties = new();

        /// <summary>创建投射物视觉对象池。</summary>
        /// <param name="pool">提供视觉实例租借和归还能力的对象池。</param>
        public ProjectileViewPool(IGameObjectPool pool)
        {
            _pool = pool;
        }

        /// <summary>按轨迹和视觉配置租借并定位一个视觉实例。</summary>
        /// <param name="projectileProfile">提供投射物轨迹与视觉颜色的配置。</param>
        /// <param name="position">视觉实例的世界坐标。</param>
        /// <param name="rotation">视觉实例的世界旋转。</param>
        /// <returns>租借的视觉实例；配置或对象池无效时返回 <c>null</c>。</returns>
        public GameObject RentProjectileView(
            ProjectileProfileSO projectileProfile,
            Vector3 position,
            Quaternion rotation)
        {
            ProjectileVisualDefinitionSO visual = projectileProfile?.VisualDefinition;
            LaserTrajectoryDefinitionSO laser = projectileProfile?.TrajectoryDefinition as
                LaserTrajectoryDefinitionSO;
            bool isLaser = laser != null;
            GameObject prefab = isLaser ? laser.BeamPrefab : visual?.ProjectilePrefab;
            if (_pool == null || prefab == null)
            {
                return null;
            }

            _prefabs.Add(prefab);
            GameObject instance = _pool.Rent(
                prefab,
                new PoolSettings(
                    isLaser ? 0 : visual.PrewarmInstanceCount,
                    isLaser
                        ? DefaultMaximumRetainedViewCount
                        : Mathf.Max(1, visual.MaximumRetainedInstanceCount)));
            if (instance == null)
            {
                return null;
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            Color tint = visual != null ? visual.TintColor : Color.white;
            LaserBeamView beamView = instance.GetComponentInChildren<LaserBeamView>(true);
            if (isLaser && beamView == null)
            {
                beamView = instance.AddComponent<LaserBeamView>();
            }

            ApplyTint(instance, tint);
            if (isLaser)
            {
                beamView?.Prepare(tint);
            }

            return instance;
        }

        /// <summary>将视觉色调写入材质属性块，不实例化材质。</summary>
        /// <param name="instance">刚从对象池租出的弹体视觉。</param>
        /// <param name="tint">视觉定义中的目标色调。</param>
        private void ApplyTint(GameObject instance, Color tint)
        {
            _renderers.Clear();
            instance.GetComponentsInChildren(true, _renderers);
            for (int rendererIndex = 0; rendererIndex < _renderers.Count; rendererIndex++)
            {
                Renderer renderer = _renderers[rendererIndex];
                renderer.GetPropertyBlock(_tintProperties);
                _tintProperties.SetColor(BaseColorPropertyId, tint);
                _tintProperties.SetColor(ColorPropertyId, tint);
                renderer.SetPropertyBlock(_tintProperties);
            }
            _renderers.Clear();
        }

        /// <summary>归还一个投射物视觉实例。</summary>
        /// <param name="projectileViewInstance">要归还的视觉实例。</param>
        public void ReturnProjectileView(GameObject projectileViewInstance)
        {
            if (projectileViewInstance != null)
            {
                LaserBeamView beamView = projectileViewInstance
                    .GetComponentInChildren<LaserBeamView>(true);
                beamView?.ResetForPool();
                _pool?.Return(projectileViewInstance);
            }
        }

        /// <summary>清理所有已登记预制体的视觉对象池。</summary>
        public void ClearAllProjectileViews()
        {
            if (_pool == null)
            {
                return;
            }

            foreach (GameObject prefab in _prefabs)
            {
                _pool.Clear(prefab);
            }
            _prefabs.Clear();
        }

        /// <summary>释放对象池中的视觉对象。</summary>
        public void Dispose() => ClearAllProjectileViews();
    }
}
