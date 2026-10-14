using System;
using CombatSystem;
using EnemySystem.Components;
using FlockingSystem.ECS;
using SpatialIndexing;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>集中协调敌人的 Buff 服务、抗性缓存与移动效果。</summary>
    internal sealed class EnemyBuffMotionEffects
    {
        private readonly Enemy _enemy;
        private readonly Func<IBuffService> _buffServiceProvider;
        private readonly Func<EnemyFlockBridge> _flockBridgeProvider;
        private readonly Func<float> _speedMultiplierProvider;
        private readonly Func<SpatialQueryService> _spatialServiceProvider;
        private readonly Action<int> _setSpatialSlot;
        private readonly Action _cacheRequiredComponents;

        private EnemyShieldComponent _shieldComponent;
        private bool _shieldLookupComplete;

        /// <summary>保存宿主和按需读取的注入依赖。</summary>
        internal EnemyBuffMotionEffects(
            Enemy enemy,
            Func<IBuffService> buffServiceProvider,
            Func<EnemyFlockBridge> flockBridgeProvider,
            Func<float> speedMultiplierProvider,
            Func<SpatialQueryService> spatialServiceProvider,
            Action<int> setSpatialSlot,
            Action cacheRequiredComponents)
        {
            _enemy = enemy;
            _buffServiceProvider = buffServiceProvider;
            _flockBridgeProvider = flockBridgeProvider;
            _speedMultiplierProvider = speedMultiplierProvider;
            _spatialServiceProvider = spatialServiceProvider;
            _setSpatialSlot = setSpatialSlot;
            _cacheRequiredComponents = cacheRequiredComponents;
        }

        /// <summary>读取护盾组件并判断敌人是否可接收有害效果。</summary>
        internal bool CanReceiveHarmfulEffects
        {
            get
            {
                if (!_shieldLookupComplete)
                {
                    _enemy.TryGetComponent(out _shieldComponent);
                    _shieldLookupComplete = true;
                }

                return _shieldComponent == null || !_shieldComponent.IsShieldActive;
            }
        }

        /// <summary>登记当前 Buff 目标。</summary>
        internal void RegisterTarget()
        {
            GetBuffService()?.RegisterTarget(_enemy);
        }

        /// <summary>注销当前 Buff 目标。</summary>
        internal void UnregisterTarget()
        {
            GetBuffService()?.UnregisterTarget(_enemy);
        }

        /// <summary>清除对象池复用前残留的临时与永久 Buff。</summary>
        internal void ResetForReuse()
        {
            IBuffService buffService = GetBuffService();
            buffService?.ClearAllBuffs(_enemy);
            buffService?.ClearAllPermanentMovementSpeedMultipliers(_enemy);
            UnregisterTarget();
        }

        /// <summary>清除护盾查询缓存，使复用实体重新确认可选组件。</summary>
        internal void ResetControlResistanceCache()
        {
            _shieldComponent = null;
            _shieldLookupComplete = false;
        }

        /// <summary>登记敌人的空间查询槽位并同步 ECS 桥接。</summary>
        internal void RegisterSpatial()
        {
            SpatialQueryService spatialService = _spatialServiceProvider();
            if (_enemy.SpatialSlot < 0 && _enemy.isActiveAndEnabled &&
                _enemy.HasConfiguration && !_enemy.IsDead && spatialService != null)
            {
                int spatialSlot = spatialService.RegisterEnemy(_enemy);
                _setSpatialSlot(spatialSlot);
                _flockBridgeProvider()?.SyncSpatialSlot(spatialSlot);
            }
        }

        /// <summary>释放空间查询槽位并同步 ECS 桥接。</summary>
        internal void UnregisterSpatial()
        {
            int previousSlot = _enemy.SpatialSlot;
            _setSpatialSlot(-1);
            _flockBridgeProvider()?.SyncSpatialSlot(-1);
            _spatialServiceProvider()?.UnregisterEnemy(previousSlot);
        }

        /// <summary>合成生成倍率与当前移动速度 Buff 倍率。</summary>
        internal float GetMovementSpeedMultiplier()
        {
            float buffMultiplier = GetBuffService()?.GetModifierMultiplier(
                _enemy,
                BuffModifierType.MovementSpeedMultiplier) ?? 1f;

            return Mathf.Max(0f, _speedMultiplierProvider()) * Mathf.Max(0f, buffMultiplier);
        }

        /// <summary>读取承伤倍率。</summary>
        internal float GetDamageTakenMultiplier()
        {
            return GetBuffService()?.GetModifierMultiplier(
                _enemy,
                BuffModifierType.DamageTakenMultiplier) ?? 1f;
        }

        /// <summary>读取攻击伤害倍率。</summary>
        internal float GetAttackDamageMultiplier()
        {
            return GetBuffService()?.GetModifierMultiplier(
                _enemy,
                BuffModifierType.AttackDamage) ?? 1f;
        }

        /// <summary>读取攻击可用性倍率。</summary>
        internal float GetAttackAllowedMultiplier()
        {
            return GetBuffService()?.GetModifierMultiplier(
                _enemy,
                BuffModifierType.AttackAvailability) ?? 1f;
        }

        /// <summary>把合成后的速度倍率同步到 ECS 桥接。</summary>
        internal void SyncMovementSpeed()
        {
            _flockBridgeProvider()?.SetSpeedMultiplier(GetMovementSpeedMultiplier());
        }

        /// <summary>提交单次运动速度倍率。</summary>
        internal void SetMovementSpeedMultiplier(float multiplier)
        {
            _flockBridgeProvider()?.SetSpeedMultiplier(Mathf.Max(0f, multiplier));
        }

        /// <summary>提交外部冲量；没有 ECS 代理时直接移动宿主。</summary>
        internal void AddExternalImpulse(Vector3 impulse)
        {
            if (impulse.sqrMagnitude <= 0f)
            {
                return;
            }

            _cacheRequiredComponents();
            EnemyFlockBridge flockBridge = _flockBridgeProvider();
            if (flockBridge != null && flockBridge.IsEcsControlled)
            {
                flockBridge.AddExternalImpulse(impulse);
                return;
            }

            _enemy.transform.position += impulse;
        }

        /// <summary>设置指定来源的永久速度倍率。</summary>
        internal void SetPermanentMovementSpeedMultiplier(float multiplier, int sourceInstanceId)
        {
            GetBuffService()?.SetPermanentMovementSpeedMultiplier(
                _enemy,
                multiplier,
                sourceInstanceId);
        }

        /// <summary>清除指定来源的永久速度倍率。</summary>
        internal void ClearPermanentMovementSpeedMultiplier(int sourceInstanceId)
        {
            GetBuffService()?.ClearPermanentMovementSpeedMultiplier(_enemy, sourceInstanceId);
        }

        /// <summary>以指定来源应用 Buff，并确保目标已登记。</summary>
        internal void ApplyBuff(BuffDefinitionSO definition, int sourceInstanceId)
        {
            if (definition == null)
            {
                return;
            }

            RegisterTarget();
            GetBuffService()?.ApplyBuff(_enemy, definition, sourceInstanceId);
        }

        /// <summary>目标没有同类 Buff 时应用定义并返回是否新增。</summary>
        internal bool TryApplyBuffIfAbsent(BuffDefinitionSO definition, int sourceInstanceId)
        {
            if (definition == null)
            {
                return false;
            }

            RegisterTarget();
            IBuffService buffService = GetBuffService();
            return buffService != null &&
                buffService.TryApplyBuffIfAbsent(_enemy, definition, sourceInstanceId);
        }

        /// <summary>移动速度 Buff 变化时刷新 ECS 速度状态。</summary>
        internal void OnBuffModifiersChanged(BuffModifierType modifierType)
        {
            if (modifierType == BuffModifierType.MovementSpeedMultiplier)
            {
                SyncMovementSpeed();
            }
        }

        /// <summary>读取注入的 Buff 服务；未注入时返回 null。</summary>
        private IBuffService GetBuffService() => _buffServiceProvider();
    }
}
