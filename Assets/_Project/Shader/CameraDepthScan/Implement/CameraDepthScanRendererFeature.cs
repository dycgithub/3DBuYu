using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShaderSystem.CameraDepthScan
{
    /// <summary>
    /// 把 CameraDepthScanRenderPass 注入指定 URP Renderer。
    /// 默认在透明物体前执行，使场景深度也能供 EnergyShield 的透明材质读取。
    /// 不自动创建材质或修改 Renderer Data，材质由 Renderer Feature Inspector 提供。
    /// </summary>
    public sealed class CameraDepthScanRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material scanMaterial;
        [SerializeField] private RenderPassEvent renderPassEvent =
            RenderPassEvent.BeforeRenderingTransparents;

        private CameraDepthScanRenderPass _renderPass;

        /// <summary>
        /// 创建并配置全屏扫描 Pass。
        /// </summary>
        public override void Create()
        {
            _renderPass ??= new CameraDepthScanRenderPass();
            _renderPass.renderPassEvent = renderPassEvent;
        }

        /// <summary>
        /// 在材质有效时把扫描 Pass 加入当前相机的渲染队列。
        /// </summary>
        /// <param name="renderer">当前相机使用的 URP Renderer。</param>
        /// <param name="renderingData">当前相机渲染数据。</param>
        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (scanMaterial == null)
            {
                return;
            }

            Create();
            _renderPass.Setup(scanMaterial);
            renderer.EnqueuePass(_renderPass);
        }
    }
}
