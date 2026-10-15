using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace CombatSystem
{
    /// <summary>
    /// 管理投射物与激光视觉的租借、外观配置和完整回池清理。
    /// </summary>
    public sealed class ProjectileViewPool : IDisposable
    {
        private const int DefaultMaximumRetainedViewCount = 256;
        private const float BrightHeadBlend = 0.65f;
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int CoreColorPropertyId = Shader.PropertyToID("_CoreColor");
        private static readonly int SegmentCountPropertyId = Shader.PropertyToID("_SegmentCount");
        private static readonly int SegmentLengthPropertyId = Shader.PropertyToID("_SegmentLength");
        private static readonly int ScrollSpeedPropertyId = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int StartFadePropertyId = Shader.PropertyToID("_StartFade");
        private static readonly int EndFadePropertyId = Shader.PropertyToID("_EndFade");
        private static readonly int EdgeSoftnessPropertyId = Shader.PropertyToID("_EdgeSoftness");

        private readonly IGameObjectPool _pool;
        private readonly HashSet<GameObject> _prefabs = new();
        private readonly List<Renderer> _renderers = new();
        private readonly List<TrailRenderer> _trailRenderers = new();
        private readonly MaterialPropertyBlock _tintProperties = new();

        /// <summary>创建投射物视觉对象池。</summary>
        /// <param name="pool">提供视觉实例租借和归还能力的对象池。</param>
        public ProjectileViewPool(IGameObjectPool pool)
        {
            _pool = pool;
        }

        /// <summary>租借一个保持未激活的视觉，并先清理、定位和配置后交给批次提交。</summary>
        /// <param name="projectileProfile">提供投射物轨迹与视觉颜色的配置。</param>
        /// <param name="position">视觉实例的世界坐标。</param>
        /// <param name="rotation">视觉实例的世界旋转。</param>
        /// <returns>已配置但未激活的视觉实例；配置或对象池无效时返回 <c>null</c>。</returns>
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
                        : Mathf.Max(1, visual.MaximumRetainedInstanceCount)),
                activate: false);
            if (instance == null)
            {
                return null;
            }

            ClearTrails(instance);
            instance.transform.SetPositionAndRotation(position, rotation);
            Color tint = visual != null ? visual.TintColor : Color.white;
            if (!isLaser && visual != null)
            {
                ApplyProjectileScale(instance, prefab, visual);
            }

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
            else if (visual != null)
            {
                ConfigureTrails(instance, visual);
            }

            return instance;
        }

        /// <summary>按定义缩放弹体根节点的首个视觉子节点，不改变根碰撞体。</summary>
        /// <param name="instance">待设置的弹体实例。</param>
        /// <param name="prefab">用于读取视觉子节点原始比例的预制体。</param>
        /// <param name="visual">提供长度和粗细倍率的视觉配置。</param>
        private static void ApplyProjectileScale(
            GameObject instance,
            GameObject prefab,
            ProjectileVisualDefinitionSO visual)
        {
            if (instance.transform.childCount == 0 || prefab.transform.childCount == 0)
            {
                return;
            }

            Transform instanceBody = instance.transform.GetChild(0);
            Vector3 prefabScale = prefab.transform.GetChild(0).localScale;
            float thickness = Mathf.Max(0.1f, visual.ProjectileThicknessMultiplier);
            float length = Mathf.Max(0.1f, visual.ProjectileLengthMultiplier);
            instanceBody.localScale = Vector3.Scale(
                prefabScale,
                new Vector3(thickness, length, thickness));
        }

        /// <summary>将视觉色调写入材质属性块，不实例化材质。</summary>
        /// <param name="instance">刚从对象池租出的投射物视觉。</param>
        /// <param name="tint">视觉定义中的目标色调。</param>
        private void ApplyTint(GameObject instance, Color tint)
        {
            _renderers.Clear();
            instance.GetComponentsInChildren(true, _renderers);
            for (int rendererIndex = 0; rendererIndex < _renderers.Count; rendererIndex++)
            {
                Renderer renderer = _renderers[rendererIndex];
                if (renderer is TrailRenderer)
                {
                    continue;
                }

                Color rendererTint = renderer.gameObject.name.Contains("Dome")
                    ? Color.Lerp(tint, Color.white, BrightHeadBlend)
                    : tint;
                renderer.GetPropertyBlock(_tintProperties);
                _tintProperties.SetColor(BaseColorPropertyId, rendererTint);
                _tintProperties.SetColor(ColorPropertyId, rendererTint);
                renderer.SetPropertyBlock(_tintProperties);
            }
            _renderers.Clear();
        }

        /// <summary>停止并清除租借实例上的全部拖尾段。</summary>
        /// <param name="instance">需要清理的投射物视觉。</param>
        private void ClearTrails(GameObject instance)
        {
            _trailRenderers.Clear();
            instance.GetComponentsInChildren(true, _trailRenderers);
            for (int trailIndex = 0; trailIndex < _trailRenderers.Count; trailIndex++)
            {
                TrailRenderer trail = _trailRenderers[trailIndex];
                trail.emitting = false;
                trail.Clear();
            }
            _trailRenderers.Clear();
        }

        /// <summary>定位后重新清除拖尾采样，再应用单层渐隐配置，避免连接旧出生点。</summary>
        /// <param name="instance">已定位的投射物视觉。</param>
        /// <param name="visual">提供拖尾颜色、宽度和寿命的视觉配置。</param>
        private void ConfigureTrails(GameObject instance, ProjectileVisualDefinitionSO visual)
        {
            _trailRenderers.Clear();
            instance.GetComponentsInChildren(true, _trailRenderers);
            Color tailEndColor = visual.TrailColor;
            tailEndColor.a = 0f;

            for (int trailIndex = 0; trailIndex < _trailRenderers.Count; trailIndex++)
            {
                TrailRenderer trail = _trailRenderers[trailIndex];
                trail.time = Mathf.Max(0f, visual.TrailTimeSeconds);
                trail.widthMultiplier = Mathf.Max(0f, visual.TrailWidth);
                trail.startColor = visual.TrailColor;
                trail.endColor = tailEndColor;

                trail.GetPropertyBlock(_tintProperties);
                _tintProperties.SetColor(BaseColorPropertyId, Color.white);
                _tintProperties.SetColor(ColorPropertyId, Color.white);
                _tintProperties.SetColor(CoreColorPropertyId, Color.white);
                _tintProperties.SetFloat(SegmentCountPropertyId, 1f);
                _tintProperties.SetFloat(SegmentLengthPropertyId, 1f);
                _tintProperties.SetFloat(ScrollSpeedPropertyId, 0f);
                _tintProperties.SetFloat(StartFadePropertyId, 0.02f);
                _tintProperties.SetFloat(EndFadePropertyId, 0.12f);
                _tintProperties.SetFloat(EdgeSoftnessPropertyId, 0.3f);
                trail.SetPropertyBlock(_tintProperties);
                trail.Clear();
                trail.emitting = visual.TrailTimeSeconds > 0f;
            }

            _trailRenderers.Clear();
        }

        /// <summary>归还一个投射物视觉实例。</summary>
        /// <param name="projectileViewInstance">要归还的视觉实例。</param>
        public void ReturnProjectileView(GameObject projectileViewInstance)
        {
            if (projectileViewInstance != null)
            {
                ClearTrails(projectileViewInstance);
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
