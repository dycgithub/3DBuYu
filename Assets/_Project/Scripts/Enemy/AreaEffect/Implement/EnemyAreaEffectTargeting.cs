using System;
using System.Collections.Generic;
using EnemySystem.AreaEffect.Model;
using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;

namespace EnemySystem.AreaEffect.Implement
{
    /// <summary>在感知间隔内查询目标，并把有效目标转换为状态机输入。</summary>
    internal sealed class EnemyAreaEffectTargeting
    {
        private const float MinimumSensingIntervalSeconds = 0.01f;

        private IDamageable _target;
        private float _elapsedSeconds;

        internal IDamageable Target => _target;

        /// <summary>按状态和固定间隔刷新当前目标，并立即清除失效引用。</summary>
        /// <param name="owner">范围精英宿主。</param>
        /// <param name="spatialService">敌人空间查询服务。</param>
        /// <param name="player">玩家战斗目标。</param>
        /// <param name="state">范围精英当前状态。</param>
        /// <param name="deltaTime">本帧经过的秒数。</param>
        internal void Tick(
            Enemy owner,
            ISpatialQueryService spatialService,
            IPlayerCombatTarget player,
            EnemyAreaEffectState state,
            float deltaTime)
        {
            if (_target != null && !IsValid(owner, _target))
            {
                _target = null;
            }

            if (state is not (EnemyAreaEffectState.Flocking or EnemyAreaEffectState.Sensing))
            {
                return;
            }

            _elapsedSeconds += Mathf.Max(0f, deltaTime);
            float interval = Mathf.Max(MinimumSensingIntervalSeconds, owner.Attributes.areaSensingInterval);
            if (_elapsedSeconds < interval)
            {
                return;
            }

            _elapsedSeconds = 0f;
            _target = Select(owner, spatialService, player);
        }

        /// <summary>建立当前目标、玩家距离和宿主生命的决策快照。</summary>
        /// <param name="owner">范围精英宿主。</param>
        /// <param name="player">玩家战斗目标。</param>
        /// <param name="deltaTime">本帧经过的秒数。</param>
        /// <returns>供状态机使用的战术输入。</returns>
        internal EnemyAreaEffectFrame CreateFrame(
            Enemy owner,
            IPlayerCombatTarget player,
            float deltaTime)
        {
            bool hasTarget = IsValid(owner, _target);
            bool hasPlayer = player != null && player.IsAlive && player.Transform != null;
            float targetDistance = hasTarget
                ? Vector3.Distance(owner.transform.position, _target.Position)
                : float.PositiveInfinity;
            float playerDistance = hasPlayer
                ? Vector3.Distance(owner.transform.position, player.Position)
                : float.PositiveInfinity;

            return new EnemyAreaEffectFrame(
                deltaTime,
                hasTarget,
                targetDistance,
                playerDistance,
                owner.HealthPercent);
        }

        /// <summary>清除租借周期的目标和感知计时。</summary>
        internal void Reset()
        {
            _target = null;
            _elapsedSeconds = 0f;
        }

        /// <summary>按精英类型选择玩家或受伤盟友，并限制在感知半径内。</summary>
        /// <param name="owner">执行范围效果的敌人。</param>
        /// <param name="spatialService">敌人空间查询服务。</param>
        /// <param name="player">玩家战斗目标。</param>
        /// <returns>有效战术目标；无目标时返回 null。</returns>
        private static IDamageable Select(
            Enemy owner,
            ISpatialQueryService spatialService,
            IPlayerCombatTarget player)
        {
            if (owner == null || owner.Attributes == null)
            {
                return null;
            }

            List<IDamageable> candidates = owner.EnemyType == EnemyType.Slow
                ? null
                : spatialService?.QueryRadius(
                    owner.transform.position,
                    Mathf.Max(0f, owner.Attributes.areaSensingRadius),
                    SpatialLayers.Enemy);
            IDamageable target = EnemyAreaEffectTargetSelector.Select(
                owner.EnemyType,
                owner,
                player,
                candidates ?? (IReadOnlyList<IDamageable>)Array.Empty<IDamageable>());
            return IsValid(owner, target) ? target : null;
        }

        /// <summary>确认目标仍存活、受伤且处于感知半径内。</summary>
        /// <param name="owner">范围精英宿主。</param>
        /// <param name="target">当前战术目标。</param>
        /// <returns>目标仍可作为当前战术目标时返回 true。</returns>
        private static bool IsValid(Enemy owner, IDamageable target)
        {
            if (owner == null || owner.Attributes == null || !IsAlive(target) ||
                (target.Position - owner.transform.position).sqrMagnitude >
                GetSensingRadiusSquared(owner.Attributes))
            {
                return false;
            }

            if (owner.EnemyType == EnemyType.Slow)
            {
                return target is IPlayerCombatTarget;
            }

            return target is Enemy ally && ally != owner && !ally.IsDead &&
                ally.CurrentHealth > 0f && ally.CurrentHealth < ally.MaxHealth;
        }

        /// <summary>计算非负感知半径的平方值。</summary>
        private static float GetSensingRadiusSquared(EnemyAttributesSO attributes)
        {
            float radius = Mathf.Max(0f, attributes.areaSensingRadius);
            return radius * radius;
        }

        /// <summary>判断目标和对应 Unity 对象仍处于有效状态。</summary>
        private static bool IsAlive(IDamageable target)
        {
            return target != null &&
                (!(target is UnityEngine.Object unityObject) || unityObject != null) &&
                target.IsAlive;
        }
    }
}
