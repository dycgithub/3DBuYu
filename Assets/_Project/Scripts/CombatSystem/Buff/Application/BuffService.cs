using System;
using Interfaces;
using Services;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace CombatSystem
{
    /// <summary>向外提供 Buff 服务，并把时间推进委托给内部应用服务。</summary>
    public sealed class BuffService : IFixedTickable, IDisposable, IBuffService
    {
        private static readonly ProfilerMarker FixedTickMarker =
            new("3DBuYu/VContainer/BuffService.FixedTick");
        private static readonly ProfilerMarker ProcessMarker =
            new("3DBuYu/BuffService.Process");

        private readonly BuffApplicationService _service;

        /// <summary>创建 Buff 服务并接入伤害提交和可选池化视效。</summary>
        public BuffService(
            IDamageRequestProcessor damageProcessor = null,
            IPooledEffectService pooledEffectService = null)
        {
            _service = new BuffApplicationService(damageProcessor, pooledEffectService);
        }

        /// <summary>登记当前可接收 Buff 的目标。</summary>
        /// <param name="target">待登记的目标。</param>
        public void RegisterTarget(IBuffTarget target)
        {
            _service.RegisterTarget(target);
        }

        /// <summary>注销目标，并清理该目标上的所有效果。</summary>
        /// <param name="target">待注销的目标。</param>
        public void UnregisterTarget(IBuffTarget target)
        {
            _service.UnregisterTarget(target);
        }

        /// <summary>固定时间推进入口，由 VContainer 的固定 PlayerLoop 调用。</summary>
        public void FixedTick()
        {
            using var profilerScope = FixedTickMarker.Auto();
            Process(Time.fixedDeltaTime);
        }

        /// <summary>按给定时间推进集中管理的周期 Buff。</summary>
        /// <param name="deltaTime">经过的秒数，负值按零处理。</param>
        public void Process(float deltaTime)
        {
            using var profilerScope = ProcessMarker.Auto();
            _service.Process(deltaTime);
        }

        /// <summary>向目标应用一个 Buff。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="definition">Buff 静态定义。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        public void ApplyBuff(
            IBuffTarget target,
            BuffDefinitionSO definition,
            int sourceInstanceId)
        {
            _service.ApplyBuff(target, definition, sourceInstanceId);
        }

        /// <summary>施加一个独立计时的火焰持续伤害层。</summary>
        /// <param name="target">持续伤害目标。</param>
        /// <param name="damagePerSecond">该层每秒伤害。</param>
        /// <param name="durationSeconds">该层持续时间。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        public void ApplyDamageOverTime(
            IBuffTarget target,
            float damagePerSecond,
            float durationSeconds,
            int sourceInstanceId)
        {
            _service.ApplyDamageOverTime(
                target,
                damagePerSecond,
                durationSeconds,
                sourceInstanceId);
        }

        /// <summary>仅在目标没有同类活动 Buff 时新增一个 Buff。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="definition">Buff 静态定义。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        /// <returns>本次实际新增时返回 true。</returns>
        public bool TryApplyBuffIfAbsent(
            IBuffTarget target,
            BuffDefinitionSO definition,
            int sourceInstanceId)
        {
            return _service.TryApplyBuffIfAbsent(target, definition, sourceInstanceId);
        }

        /// <summary>查询目标指定属性的合成倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="modifierType">属性类别。</param>
        /// <returns>没有匹配状态时返回一。</returns>
        public float GetModifierMultiplier(
            IBuffTarget target,
            BuffModifierType modifierType)
        {
            return _service.GetModifierMultiplier(target, modifierType);
        }

        /// <summary>移除目标上指定来源创建的全部临时 Buff。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        public void RemoveBySourceId(IBuffTarget target, int sourceInstanceId)
        {
            _service.RemoveBySourceId(target, sourceInstanceId);
        }

        /// <summary>清除目标上的全部临时 Buff。</summary>
        /// <param name="target">Buff 目标。</param>
        public void ClearAllBuffs(IBuffTarget target)
        {
            _service.ClearAllBuffs(target);
        }

        /// <summary>设置目标指定来源的永久移动速度倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="multiplier">移动速度倍率。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        public void SetPermanentMovementSpeedMultiplier(
            IBuffTarget target,
            float multiplier,
            int sourceInstanceId)
        {
            _service.SetPermanentMovementSpeedMultiplier(
                target,
                multiplier,
                sourceInstanceId);
        }

        /// <summary>移除目标指定来源的永久移动速度倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        public void ClearPermanentMovementSpeedMultiplier(
            IBuffTarget target,
            int sourceInstanceId)
        {
            _service.ClearPermanentMovementSpeedMultiplier(target, sourceInstanceId);
        }

        /// <summary>清除目标上的全部永久移动速度倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        public void ClearAllPermanentMovementSpeedMultipliers(IBuffTarget target)
        {
            _service.ClearAllPermanentMovementSpeedMultipliers(target);
        }

        /// <summary>按来源清除场景内所有目标的永久移动速度倍率。</summary>
        /// <param name="sourceInstanceId">永久效果来源实例标识。</param>
        public void ClearPermanentMovementSpeedMultipliersBySourceId(int sourceInstanceId)
        {
            _service.ClearPermanentMovementSpeedMultipliersBySourceId(sourceInstanceId);
        }

        /// <summary>释放所有 Buff 目标状态和调度资源。</summary>
        public void Dispose()
        {
            _service.Dispose();
        }
    }
}
