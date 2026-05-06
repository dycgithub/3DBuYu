using SphereMovement.Data;
using SphereMovement.Environment;
using SphereMovement.Interfaces;
using System;
using UnityEngine;

namespace SphereMovement.Core
{
    /// <summary>
    /// 移动策略工厂
    /// 负责创建不同的移动策略实例
    /// </summary>
    public static class MovementStrategyFactory
    {
        /// <summary>
        /// 创建移动策略
        /// </summary>
        /// <param name="mode">移动模式</param>
        /// <param name="config">移动配置</param>
        /// <param name="surface">球面环境（球面模式下必需）</param>
        /// <returns>移动策略实例</returns>
        public static IMovementStrategy Create(
            MovementMode mode,
            MovementConfig config,
            SphereSurface surface = null)
        {
            return mode switch
            {
                MovementMode.Plane => new PlaneMovementStrategy(config),
                MovementMode.Spherical => CreateSphericalStrategy(config, surface),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), $"不支持的移动模式: {mode}")
            };
        }

        /// <summary>
        /// 创建平面移动策略
        /// </summary>
        public static PlaneMovementStrategy CreatePlaneStrategy(MovementConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return new PlaneMovementStrategy(config);
        }

        /// <summary>
        /// 创建球面移动策略
        /// </summary>
        public static SphericalMovementStrategy CreateSphericalStrategy(
            MovementConfig config,
            SphereSurface surface)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (surface == null)
                throw new ArgumentNullException(nameof(surface), "球面移动策略需要指定球面环境");

            return new SphericalMovementStrategy(config, surface);
        }

        /// <summary>
        /// 尝试创建移动策略
        /// </summary>
        /// <returns>是否成功创建</returns>
        public static bool TryCreate(
            MovementMode mode,
            MovementConfig config,
            SphereSurface surface,
            out IMovementStrategy strategy)
        {
            try
            {
                strategy = Create(mode, config, surface);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MovementStrategyFactory] 创建移动策略失败: {ex.Message}");
                strategy = null;
                return false;
            }
        }
    }

    /// <summary>
    /// 移动策略基类
    /// </summary>
    public abstract class MovementStrategyBase : IMovementStrategy
    {
        protected readonly MovementConfig Config;

        protected MovementStrategyBase(MovementConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public abstract void Move(Transform target, Vector2 input, float deltaTime);

        /// <summary>
        /// 从世界位置初始化策略
        /// </summary>
        public abstract void InitializeFromPosition(Vector3 worldPosition);

        /// <summary>
        /// 获取当前速度
        /// </summary>
        public abstract Vector3 GetCurrentVelocity();

        /// <summary>
        /// 是否可以在当前位置停止
        /// </summary>
        public abstract bool CanStopAtCurrentPosition();

        /// <summary>
        /// 平滑插值角度
        /// </summary>
        protected float SmoothAngle(float current, float target, ref float velocity, float smoothTime)
        {
            return Mathf.SmoothDampAngle(
                current * Mathf.Rad2Deg,
                target * Mathf.Rad2Deg,
                ref velocity,
                smoothTime
            ) * Mathf.Deg2Rad;
        }

        /// <summary>
        /// 平滑插值值
        /// </summary>
        protected float SmoothValue(float current, float target, ref float velocity, float smoothTime)
        {
            return Mathf.SmoothDamp(current, target, ref velocity, smoothTime);
        }
    }
}
