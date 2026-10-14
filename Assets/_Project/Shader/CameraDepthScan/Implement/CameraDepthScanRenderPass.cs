using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ShaderSystem.CameraDepthScan
{
    /// <summary>
    /// 将相机颜色交给 CameraDepthScan Shader，并声明其对场景深度的读取需求。
    /// Render Graph 会在当前颜色纹理和临时目标之间建立依赖，同时把深度发布给后续透明材质。
    /// </summary>
    internal sealed class CameraDepthScanRenderPass : ScriptableRenderPass
    {
        private const string PassName = "Camera Depth Scan";
        private static readonly int CameraDepthTextureId =
            Shader.PropertyToID("_CameraDepthTexture");

        private sealed class PassData
        {
            public TextureHandle source;
            public TextureHandle depth;
            public Material material;
        }

        private Material _material;

        /// <summary>
        /// 设置本次 Pass 使用的扫描材质，并请求相机深度输入。
        /// </summary>
        /// <param name="material">使用 CameraDepthScan Shader 的材质。</param>
        internal void Setup(Material material)
        {
            _material = material;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        /// <summary>
        /// 记录一次从活动相机颜色到扫描结果纹理的全屏材质 Blit。
        /// </summary>
        /// <param name="renderGraph">当前 URP Render Graph。</param>
        /// <param name="frameData">当前相机帧资源。</param>
        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (_material == null)
            {
                return;
            }

            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid())
            {
                return;
            }

            TextureHandle source = resources.activeColorTexture;
            TextureHandle depth = resources.cameraDepthTexture;
            if (!depth.IsValid())
            {
                return;
            }

            TextureDesc destinationDescription = renderGraph.GetTextureDesc(source);
            destinationDescription.name = PassName;
            destinationDescription.clearBuffer = false;
            destinationDescription.depthBufferBits = 0;
            TextureHandle destination = renderGraph.CreateTexture(destinationDescription);

            using (IRasterRenderGraphBuilder builder =
                renderGraph.AddRasterRenderPass<PassData>(PassName, out PassData passData))
            {
                passData.source = source;
                passData.depth = depth;
                passData.material = _material;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(depth, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(depth, CameraDepthTextureId);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture(CameraDepthTextureId, data.depth);
                    Blitter.BlitTexture(
                        context.cmd,
                        data.source,
                        Vector2.one,
                        data.material,
                        0);
                });
            }

            resources.cameraColor = destination;
        }
    }
}
