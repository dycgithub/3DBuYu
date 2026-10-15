using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// 管理 Flocking ECS 实例使用的网格、调色板材质和模型局部变换。
    /// 视觉资源按源预制体和视觉编号缓存，服务销毁时统一解除 Entities Graphics 注册。
    /// </summary>
    public sealed partial class EnemyFlockRuntimeService
    {
        /// <summary>按源预制体和视觉编号缓存已注册的视觉定义。</summary>
        private readonly Dictionary<EnemyFlockVisualKey, EnemyFlockVisual> _visuals = new();

        /// <summary>服务创建的运行时材质，Dispose 时统一销毁。</summary>
        private readonly List<Material> _runtimeMaterials = new();

        private bool _warnedMissingGraphics;

        /// <summary>区分不同源预制体视觉定义的缓存键。</summary>
        private readonly struct EnemyFlockVisualKey : IEquatable<EnemyFlockVisualKey>
        {
            private readonly int _sourcePrefabId;
            private readonly int _visualIndex;

            public EnemyFlockVisualKey(GameObject sourcePrefab, int visualIndex)
            {
                _sourcePrefabId = sourcePrefab != null ? sourcePrefab.GetInstanceID() : 0;
                _visualIndex = visualIndex;
            }

            public bool Equals(EnemyFlockVisualKey other)
            {
                return _sourcePrefabId == other._sourcePrefabId &&
                    _visualIndex == other._visualIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is EnemyFlockVisualKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_sourcePrefabId, _visualIndex);
            }
        }

        /// <summary>Entities Graphics 注册结果和实体初始化所需的视觉数据。</summary>
        private struct EnemyFlockVisual
        {
            public BatchMaterialID MaterialId;
            public BatchMeshID MeshId;
            public PostTransformMatrix PostTransform;
            public float4 BaseColor;
            public float4 CrackColor;
            public float4 EmissionColor;
        }

        /// <summary>提取并缓存源预制体的网格、材质和局部模型变换。</summary>
        private bool TryGetVisualDefinition(
            GameObject sourcePrefab,
            int visualIndex,
            out EnemyFlockVisual visual)
        {
            EnemyFlockVisualKey visualKey = new(sourcePrefab, visualIndex);
            if (_visuals.TryGetValue(visualKey, out visual))
            {
                return true;
            }

            EntitiesGraphicsSystem graphics = _world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (graphics == null)
            {
                if (!_warnedMissingGraphics)
                {
                    Debug.LogError("[EnemyFlock] Entities Graphics System 不存在，无法启动 ECS Flocking。");
                    _warnedMissingGraphics = true;
                }

                return false;
            }

            MeshFilter meshFilter = sourcePrefab.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer renderer = meshFilter != null
                ? meshFilter.GetComponent<MeshRenderer>()
                : sourcePrefab.GetComponentInChildren<MeshRenderer>(true);
            if (meshFilter == null || renderer == null ||
                meshFilter.sharedMesh == null || renderer.sharedMaterial == null)
            {
                Debug.LogError(
                    $"[EnemyFlock] {sourcePrefab.name} 缺少 MeshFilter/MeshRenderer，无法创建 ECS Enemy。");
                return false;
            }

            Material sourceMaterial = renderer.sharedMaterial;
            Color sourceBaseColor = ReadBaseColor(sourceMaterial);
            Color sourceCrackColor = ReadColor(
                sourceMaterial,
                "_CrackColor",
                new Color(1f, 0.2f, 0.02f, 1f));
            Color sourceEmissionColor = ReadColor(
                sourceMaterial,
                "_EmissionColor",
                new Color(1f, 0.02f, 0.01f, 1f));
            Material material = CreateFlockRenderMaterial(sourceMaterial, visualIndex);
            if (material == null)
            {
                return false;
            }

            BatchMeshID meshId = graphics.RegisterMesh(meshFilter.sharedMesh);
            BatchMaterialID materialId = graphics.RegisterMaterial(material);
            if (meshId.Equals(BatchMeshID.Null) || materialId.Equals(BatchMaterialID.Null))
            {
                if (!materialId.Equals(BatchMaterialID.Null))
                {
                    graphics.UnregisterMaterial(materialId);
                }

                if (!meshId.Equals(BatchMeshID.Null))
                {
                    graphics.UnregisterMesh(meshId);
                }

                UnityEngine.Object.Destroy(material);
                return false;
            }

            Transform model = meshFilter.transform;
            visual = new EnemyFlockVisual
            {
                MeshId = meshId,
                MaterialId = materialId,
                BaseColor = ToLinearColor(sourceBaseColor),
                CrackColor = ToLinearColor(sourceCrackColor),
                EmissionColor = ToLinearColor(sourceEmissionColor),
                PostTransform = new PostTransformMatrix
                {
                    Value = float4x4.TRS(
                        new float3(model.localPosition.x, model.localPosition.y, model.localPosition.z),
                        new quaternion(
                            model.localRotation.x,
                            model.localRotation.y,
                            model.localRotation.z,
                            model.localRotation.w),
                        new float3(model.localScale.x, model.localScale.y, model.localScale.z)),
                },
            };
            _visuals.Add(visualKey, visual);
            _runtimeMaterials.Add(material);
            return true;
        }

        /// <summary>创建复制源材质属性的 ECS 专用实例材质。</summary>
        private Material CreateFlockRenderMaterial(Material source, int visualIndex)
        {
            Shader shader = ResolveFlockShader();
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = $"EnemyFlockMaterial_{source.name}_{visualIndex}",
                enableInstancing = true,
            };

            if (!material.HasProperty("_BaseColor"))
            {
                Debug.LogError(
                    $"[EnemyFlock] Shader '{shader.name}' 缺少 _BaseColor，无法创建 ECS Enemy 材质。");
                UnityEngine.Object.Destroy(material);
                return null;
            }

            // 旧材质可能只暴露 _Color；ECS shader 统一使用 _BaseColor。
            material.SetColor("_BaseColor", ReadBaseColor(source));
            CopyTexture(source, material, "_BaseMap");
            CopyColor(source, material, "_ShadowColor");
            CopyColor(source, material, "_SpecularColor");
            CopyColor(source, material, "_RimColor");
            CopyColor(source, material, "_CrackColor");
            CopyColor(source, material, "_EmissionColor");
            CopyFloat(source, material, "_Glossiness");
            CopyFloat(source, material, "_RimPower");
            CopyFloat(source, material, "_RimThreshold");
            CopyFloat(source, material, "_ShadowStep");
            CopyFloat(source, material, "_CrackStartHealth");
            CopyFloat(source, material, "_CrackScale");
            CopyFloat(source, material, "_CrackWidth");
            CopyFloat(source, material, "_CriticalHealth");
            CopyFloat(source, material, "_CriticalPulseSpeed");
            CopyFloat(source, material, "_CriticalPulseIntensity");
            CopyFloat(source, material, "_DissolveEdgeWidth");
            return material;
        }

        /// <summary>查找受击材质，并在降级到 URP Lit 时输出一次诊断。</summary>
        private Shader ResolveFlockShader()
        {
            Shader shader = Shader.Find("Custom/EnemyFlockToon");
            if (shader != null)
            {
                return shader;
            }

            Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
            if (!_warnedShaderFallback)
            {
                Debug.LogError(
                    "[EnemyFlock] 无法找到 Custom/EnemyFlockToon，当前 ECS 敌人将降级为 URP Lit。");
                _warnedShaderFallback = true;
            }

            return fallback;
        }

        /// <summary>解除 Entities Graphics 注册并销毁服务创建的材质。</summary>
        private void DisposeVisualResources()
        {
            if (_world != null && _world.IsCreated)
            {
                EntitiesGraphicsSystem graphics =
                    _world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                foreach (EnemyFlockVisual visual in _visuals.Values)
                {
                    graphics?.UnregisterMaterial(visual.MaterialId);
                    graphics?.UnregisterMesh(visual.MeshId);
                }
            }

            foreach (Material material in _runtimeMaterials)
            {
                UnityEngine.Object.Destroy(material);
            }

            _visuals.Clear();
            _runtimeMaterials.Clear();
        }

        /// <summary>读取 URP 基础颜色，兼容只提供 _Color 的旧材质。</summary>
        private static Color ReadBaseColor(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.white;
        }

        /// <summary>读取可选颜色属性，缺失时使用视觉系统默认颜色。</summary>
        private static Color ReadColor(Material material, string property, Color fallback)
        {
            return material != null && material.HasProperty(property)
                ? material.GetColor(property)
                : fallback;
        }

        /// <summary>将 Unity 颜色转换为 ECS 材质覆盖使用的线性 RGBA。</summary>
        private static float4 ToLinearColor(Color color)
        {
            Color linearColor = color.linear;
            return new float4(
                linearColor.r,
                linearColor.g,
                linearColor.b,
                linearColor.a);
        }

        private static void CopyColor(Material source, Material target, string property)
        {
            if (source == null || target == null ||
                !source.HasProperty(property) || !target.HasProperty(property))
            {
                return;
            }

            target.SetColor(property, source.GetColor(property));
        }

        /// <summary>复制共享调色板及其 UV 变换到 ECS 专用材质。</summary>
        private static void CopyTexture(Material source, Material target, string property)
        {
            if (source == null || target == null ||
                !source.HasProperty(property) || !target.HasProperty(property))
            {
                return;
            }

            target.SetTexture(property, source.GetTexture(property));
            target.SetTextureScale(property, source.GetTextureScale(property));
            target.SetTextureOffset(property, source.GetTextureOffset(property));
        }

        private static void CopyFloat(Material source, Material target, string property)
        {
            if (source.HasProperty(property) && target.HasProperty(property))
            {
                target.SetFloat(property, source.GetFloat(property));
            }
        }
    }
}
