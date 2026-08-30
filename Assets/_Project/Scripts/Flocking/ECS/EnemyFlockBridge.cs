using Unity.Entities;
using UnityEngine;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// 将托管敌人对象与 ECS Flocking 实体、slot 和 Transform 提交索引关联起来。
    /// </summary>
    public sealed class EnemyFlockBridge : MonoBehaviour
    {
        internal EnemyFlockRuntimeService Runtime { get; private set; }
        internal Entity Entity { get; private set; }
        /// <summary>当前 ECS 群游槽位；未绑定时为 -1。</summary>
        public int Slot { get; private set; } = -1;
        internal int TransformArrayIndex { get; set; } = -1;
        private float _speedMultiplier = 1f;
        private MeshRenderer[] _sourceRenderers;

        public bool IsEcsControlled => Runtime != null && Entity != Entity.Null;

        public void SetSpeedMultiplier(float multiplier)
        {
            if (!IsEcsControlled || Mathf.Abs(_speedMultiplier - multiplier) < 0.0001f)
                return;

            _speedMultiplier = multiplier;
            Runtime.SetSpeedMultiplier(this, multiplier);
        }

        internal void Attach(
            EnemyFlockRuntimeService runtime,
            Entity entity,
            int slot,
            int transformArrayIndex,
            float speedMultiplier)
        {
            Runtime = runtime;
            Entity = entity;
            Slot = slot;
            TransformArrayIndex = transformArrayIndex;
            _speedMultiplier = speedMultiplier;
        }

        /// <summary>
        /// 隐藏原预制体的 MeshRenderer，避免 ECS 实例与 GameObject 视觉重复绘制。
        /// </summary>
        internal void SetEcsPresentation(bool enabled)
        {
            if (_sourceRenderers == null)
            {
                if (!enabled)
                    return;
                _sourceRenderers = GetComponentsInChildren<MeshRenderer>(true);
            }

            foreach (MeshRenderer renderer in _sourceRenderers)
                if (renderer != null)
                    renderer.enabled = !enabled;
        }

        internal void SetSpeedMultiplierCache(float multiplier)
        {
            _speedMultiplier = multiplier;
        }

        internal void ClearBinding()
        {
            SetEcsPresentation(false);
            Runtime = null;
            Entity = Entity.Null;
            Slot = -1;
            TransformArrayIndex = -1;
        }

        private void OnDestroy()
        {
            Runtime?.Unbind(this);
        }
    }
}
