using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>以连续主光束和亮芯显示激光投射物，并在回池时清除残留线段。</summary>
    public sealed class LaserBeamView : MonoBehaviour
    {
        private const float DefaultBeamWidth = 0.1f;
        private const float CoreWidthRatio = 0.35f;
        private const float CoreTintBlend = 0.8f;
        private const float OuterBeamAlpha = 0.28f;
        private const float OuterEdgeSoftness = 0.42f;
        private const float OuterCoreWidth = 0.2f;
        private const float CoreEdgeSoftness = 0.06f;
        private const float ContinuousSegmentCount = 1f;
        private const float ContinuousSegmentLength = 1f;
        private const float StaticScrollSpeed = 0f;
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int CoreColorPropertyId = Shader.PropertyToID("_CoreColor");
        private static readonly int SegmentCountPropertyId = Shader.PropertyToID("_SegmentCount");
        private static readonly int SegmentLengthPropertyId = Shader.PropertyToID("_SegmentLength");
        private static readonly int ScrollSpeedPropertyId = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int EdgeSoftnessPropertyId = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int CoreWidthPropertyId = Shader.PropertyToID("_CoreWidth");
        private static readonly int StartFadePropertyId = Shader.PropertyToID("_StartFade");
        private static readonly int EndFadePropertyId = Shader.PropertyToID("_EndFade");

        private readonly List<LineRenderer> _lineRenderers = new();
        private readonly Vector3[] _beamPoints = new Vector3[2];
        private MaterialPropertyBlock _neutralMaterialProperties;
        private LineRenderer _mainBeam;
        private LineRenderer _brightCore;

        /// <summary>读取 Beam 预制体中的外束与亮芯，缺失亮芯时补充第二条线。</summary>
        private void Awake()
        {
            _neutralMaterialProperties = new MaterialPropertyBlock();
            EnsureRenderers();
            ResetForPool();
        }

        /// <summary>准备连续亮芯与柔和外束，不叠加额外渲染层。</summary>
        /// <param name="tint">激光视觉定义提供的颜色。</param>
        internal void Prepare(Color tint)
        {
            EnsureRenderers();
            _mainBeam.enabled = true;
            _brightCore.enabled = true;
            _mainBeam.positionCount = 0;
            _brightCore.positionCount = 0;
            Color outerTint = tint;
            outerTint.a *= OuterBeamAlpha;
            _mainBeam.startColor = outerTint;
            _mainBeam.endColor = outerTint;

            Color coreTint = Color.Lerp(tint, Color.white, CoreTintBlend);
            coreTint.a = tint.a;
            _brightCore.startColor = coreTint;
            _brightCore.endColor = coreTint;
            _brightCore.startWidth = _mainBeam.startWidth * CoreWidthRatio;
            _brightCore.endWidth = _mainBeam.endWidth * CoreWidthRatio;
            _brightCore.widthMultiplier = _mainBeam.widthMultiplier;
            SetBeamMaterial(_mainBeam, OuterEdgeSoftness, OuterCoreWidth);
            SetBeamMaterial(_brightCore, CoreEdgeSoftness, 1f);
        }

        /// <summary>逐步更新连续光束的起点和当前终点。</summary>
        /// <param name="startPosition">本次激光发射位置。</param>
        /// <param name="endPosition">当前模拟位置。</param>
        internal void SetBeam(Vector3 startPosition, Vector3 endPosition)
        {
            EnsureRenderers();
            _beamPoints[0] = startPosition;
            _beamPoints[1] = endPosition;
            _mainBeam.positionCount = _beamPoints.Length;
            _brightCore.positionCount = _beamPoints.Length;
            _mainBeam.SetPositions(_beamPoints);
            _brightCore.SetPositions(_beamPoints);
        }

        /// <summary>隐藏线段并清除位置数据，防止对象池复用时残留旧光束。</summary>
        internal void ResetForPool()
        {
            if (_mainBeam != null)
            {
                _mainBeam.positionCount = 0;
                _mainBeam.enabled = false;
            }

            if (_brightCore != null)
            {
                _brightCore.positionCount = 0;
                _brightCore.enabled = false;
            }
        }

        /// <summary>复用预制体线段，确保每个 Beam 都有主束与亮芯。</summary>
        private void EnsureRenderers()
        {
            if (_mainBeam != null && _brightCore != null)
            {
                return;
            }

            _lineRenderers.Clear();
            GetComponentsInChildren(true, _lineRenderers);
            if (_mainBeam == null && _lineRenderers.Count > 0)
            {
                _mainBeam = _lineRenderers[0];
            }

            if (_mainBeam == null)
            {
                _mainBeam = gameObject.AddComponent<LineRenderer>();
                _mainBeam.startWidth = DefaultBeamWidth;
                _mainBeam.endWidth = DefaultBeamWidth;
            }

            if (_brightCore == null)
            {
                for (int index = 0; index < _lineRenderers.Count; index++)
                {
                    if (_lineRenderers[index] != _mainBeam)
                    {
                        _brightCore = _lineRenderers[index];
                        break;
                    }
                }
            }

            if (_brightCore == null)
            {
                GameObject coreObject = new GameObject("BrightCore");
                coreObject.transform.SetParent(transform, false);
                _brightCore = coreObject.AddComponent<LineRenderer>();
                _brightCore.sharedMaterial = _mainBeam.sharedMaterial;
            }

            _mainBeam.useWorldSpace = true;
            _mainBeam.loop = false;
            _brightCore.useWorldSpace = true;
            _brightCore.loop = false;
        }

        /// <summary>让光束渐变控制颜色，并覆盖共享材质的分段脉冲参数。</summary>
        /// <param name="lineRenderer">需要显示为连续光束的线段。</param>
        /// <param name="edgeSoftness">横截面边缘的柔化比例。</param>
        /// <param name="coreWidth">线段内部亮芯的宽度比例。</param>
        private void SetBeamMaterial(
            LineRenderer lineRenderer,
            float edgeSoftness,
            float coreWidth)
        {
            _neutralMaterialProperties ??= new MaterialPropertyBlock();
            lineRenderer.GetPropertyBlock(_neutralMaterialProperties);
            _neutralMaterialProperties.SetColor(BaseColorPropertyId, Color.white);
            _neutralMaterialProperties.SetColor(ColorPropertyId, Color.white);
            _neutralMaterialProperties.SetColor(CoreColorPropertyId, Color.white);
            _neutralMaterialProperties.SetFloat(SegmentCountPropertyId, ContinuousSegmentCount);
            _neutralMaterialProperties.SetFloat(SegmentLengthPropertyId, ContinuousSegmentLength);
            _neutralMaterialProperties.SetFloat(ScrollSpeedPropertyId, StaticScrollSpeed);
            _neutralMaterialProperties.SetFloat(EdgeSoftnessPropertyId, edgeSoftness);
            _neutralMaterialProperties.SetFloat(CoreWidthPropertyId, coreWidth);
            _neutralMaterialProperties.SetFloat(StartFadePropertyId, 0.02f);
            _neutralMaterialProperties.SetFloat(EndFadePropertyId, 0.08f);
            lineRenderer.SetPropertyBlock(_neutralMaterialProperties);
        }
    }
}
