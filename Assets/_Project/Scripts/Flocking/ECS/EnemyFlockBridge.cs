using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// 将托管敌人对象与 ECS Flocking 实体、slot 和 Transform 提交索引关联起来。
    /// 每个敌人只允许一个桥接组件，避免多个桥接同时驱动同一宿主。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyFlockBridge : MonoBehaviour
    {
        /// <summary>当前绑定所属的 ECS 运行服务。</summary>
        internal EnemyFlockRuntimeService Runtime { get; private set; }

        /// <summary>当前绑定的 ECS 实体。</summary>
        internal Entity Entity { get; private set; }

        /// <summary>当前 ECS 群游槽位；未绑定时为 -1。</summary>
        public int Slot { get; private set; } = -1;

        /// <summary>提交系统中对应的 TransformAccessArray 索引；未绑定时为 -1。</summary>
        internal int TransformArrayIndex { get; set; } = -1;

        /// <summary>最后一次写入 ECS 的速度倍率，用于过滤重复命令。</summary>
        private float _speedMultiplier = 1f;

        /// <summary>绑定时缓存的线性基础颜色，用于恢复表现状态。</summary>
        private float4 _baseColor = new(1f, 1f, 1f, 1f);

        /// <summary>绑定时缓存的线性裂纹颜色。</summary>
        private float4 _crackColor = new(1f, 0.2f, 0.02f, 1f);

        /// <summary>绑定时缓存的线性发光颜色。</summary>
        private float4 _emissionColor = new(1f, 0.02f, 0.01f, 1f);

        /// <summary>当前 ECS 绑定的基础颜色。</summary>
        internal float4 CachedBaseColor => _baseColor;

        /// <summary>当前 ECS 绑定的裂纹颜色。</summary>
        internal float4 CachedCrackColor => _crackColor;

        /// <summary>当前 ECS 绑定的发光颜色。</summary>
        internal float4 CachedEmissionColor => _emissionColor;

        /// <summary>源预制体下需要隐藏或恢复的 MeshRenderer 缓存。</summary>
        private MeshRenderer[] _sourceRenderers;

        /// <summary>当前是否已绑定有效的 ECS 群游实体。</summary>
        public bool IsEcsControlled => Runtime != null && Entity != Entity.Null;

        /// <summary>
        /// 将托管敌人的速度倍率同步到 ECS 群游实体。
        /// </summary>
        /// <param name="multiplier">要应用的速度倍率。</param>
        public void SetSpeedMultiplier(float multiplier)
        {
            if (!IsEcsControlled || Mathf.Abs(_speedMultiplier - multiplier) < 0.0001f)
            {
                return;
            }

            _speedMultiplier = multiplier;
            Runtime.UpdateAgentSpeedMultiplier(this, multiplier);
        }

        /// <summary>把一次外部速度冲量提交给 ECS 代理状态。</summary>
        /// <param name="impulse">单位为世界单位/秒的冲量。</param>
        public void AddExternalImpulse(Vector3 impulse)
        {
            if (!IsEcsControlled || impulse.sqrMagnitude <= 0f)
            {
                return;
            }

            Runtime.QueueAgentExternalImpulse(
                this,
                new float3(impulse.x, impulse.y, impulse.z));
        }

        /// <summary>
        /// 设置是否由外部行为组件驱动 Transform；启用后群游只消费外部冲量。
        /// </summary>
        /// <param name="enabled">是否暂停群游位移并保留外部 Transform 控制。</param>
        public void SetExternalMotion(bool enabled)
        {
            if (IsEcsControlled)
            {
                Runtime.UpdateAgentExternalMotion(this, enabled);
            }
        }

        /// <summary>通知 ECS 当前托管敌人的空间查询槽位已经变化。</summary>
        internal void SyncSpatialSlot(int spatialSlot)
        {
            Runtime?.UpdateAgentSpatialSlot(this, spatialSlot);
        }

        /// <summary>
        /// 保存托管敌人与 ECS 实体、slot 和 Transform 索引之间的绑定关系。
        /// </summary>
        /// <param name="runtime">拥有该绑定的运行时服务。</param>
        /// <param name="entity">对应的 ECS 实体。</param>
        /// <param name="slot">用于快照数组的代理 slot。</param>
        /// <param name="transformArrayIndex">TransformAccessArray 中的索引。</param>
        /// <param name="speedMultiplier">当前速度倍率。</param>
        internal void AttachEcsAgent(
            EnemyFlockRuntimeService runtime,
            Entity entity,
            int slot,
            int transformArrayIndex,
            float speedMultiplier,
            float4 baseColor)
        {
            Runtime = runtime;
            Entity = entity;
            Slot = slot;
            TransformArrayIndex = transformArrayIndex;
            _speedMultiplier = speedMultiplier;
            _baseColor = baseColor;
        }

        /// <summary>
        /// 设置是否隐藏原预制体的 Renderer，避免 ECS 实例重复绘制。
        /// </summary>
        /// <param name="enabled">为 true 时隐藏原预制体 Renderer。</param>
        internal void SetEcsPresentationEnabled(bool enabled)
        {
            if (_sourceRenderers == null)
            {
                if (!enabled)
                {
                    return;
                }

                _sourceRenderers = GetComponentsInChildren<MeshRenderer>(true);
            }

            foreach (MeshRenderer renderer in _sourceRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = !enabled;
                }
            }
        }

        /// <summary>
        /// 缓存已写入 ECS 的速度倍率，避免重复写入组件数据。
        /// </summary>
        /// <param name="multiplier">要缓存的速度倍率。</param>
        internal void CacheSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = multiplier;
        }

        /// <summary>缓存当前 ECS 视觉定义的基础颜色。</summary>
        /// <param name="baseColor">线性空间基础颜色。</param>
        internal void CacheBaseColor(float4 baseColor)
        {
            _baseColor = baseColor;
        }

        /// <summary>缓存当前 ECS 视觉定义的裂纹和发光颜色。</summary>
        internal void CacheVisualColors(float4 crackColor, float4 emissionColor)
        {
            _crackColor = crackColor;
            _emissionColor = emissionColor;
        }

        /// <summary>把集中视觉系统的实例状态转发给 ECS 实体。</summary>
        /// <param name="state">要写入的视觉状态。</param>
        internal void SetVisualState(EnemyFlockVisualState state)
        {
            Runtime?.UpdateAgentVisualState(this, state);
        }

        /// <summary>将表现颜色转发给 ECS 实体的材质属性覆盖。</summary>
        /// <param name="color">要显示的颜色。</param>
        internal void SetPresentationColor(Color color)
        {
            Runtime?.SetAgentPresentationColor(this, ToLinearColor(color));
        }

        /// <summary>恢复 ECS 实体绑定前的基础颜色。</summary>
        internal void ResetPresentationColor()
        {
            Runtime?.ResetAgentPresentationColor(this, _baseColor);
        }

        /// <summary>
        /// 清除 ECS 绑定，并按调用方要求决定是否恢复托管预制体的 Renderer。
        /// </summary>
        /// <param name="restoreSourceRenderer">是否恢复源 MeshRenderer。</param>
        internal void ClearEcsBinding(bool restoreSourceRenderer = true)
        {
            if (restoreSourceRenderer)
            {
                SetEcsPresentationEnabled(false);
            }

            Runtime = null;
            Entity = Entity.Null;
            Slot = -1;
            TransformArrayIndex = -1;
            _baseColor = new float4(1f, 1f, 1f, 1f);
            _crackColor = new float4(1f, 0.2f, 0.02f, 1f);
            _emissionColor = new float4(1f, 0.02f, 0.01f, 1f);
        }

        /// <summary>
        /// 销毁桥接对象前通知运行时服务解除绑定。
        /// </summary>
        private void OnDestroy()
        {
            Runtime?.UnbindAgent(this);
        }

        /// <summary>将 Unity 的伽马空间颜色转换为 ECS 材质覆盖使用的线性颜色。</summary>
        private static float4 ToLinearColor(Color color)
        {
            Color linearColor = color.linear;
            return new float4(
                linearColor.r,
                linearColor.g,
                linearColor.b,
                linearColor.a);
        }
    }
}
