using System;
using System.Collections.Generic;
using CombatSystem;
using EffectSystem;
using EnemySystem.Components;
using FlockingSystem.ECS;
using R3;
using Services;
using Unity.Mathematics;
using UnityEngine;
using VContainer.Unity;

namespace EnemySystem.Visual
{
    /// <summary>
    /// 集中管理敌人 ECS 实例的受击、死亡溶解和冻结状态。
    /// <para>池化重置恢复群游，死亡期间由 ECS 保持最终姿态不动。</para>
    /// </summary>
    public sealed class EnemyVisualSystem : ITickable, IDisposable
    {
        private sealed class Entry
        {
            public Enemy Enemy;
            public EnemyFlockBridge Bridge;
            public EnemyDamagePresentationComponent Presentation;
            public EnemyFlockVisualState State;
            public float FlashDuration;
            public float DeathDuration;
            public bool IsDying;
            public bool IsInitialized;
            public bool IsDirty;
            public IDisposable HealthSubscription;
        }

        private readonly IDamageEventSource _damageEventSource;
        private readonly IEffectService _effectService;
        private readonly Dictionary<Enemy, Entry> _entries = new();
        private readonly List<Entry> _iterationBuffer = new(128);
        private bool _disposed;

        /// <summary>创建视觉系统并订阅统一实际扣血事件。</summary>
        public EnemyVisualSystem(
            IDamageEventSource damageEventSource,
            IEffectService effectService)
        {
            _damageEventSource = damageEventSource;
            _effectService = effectService;
            if (_damageEventSource != null)
            {
                _damageEventSource.DamageApplied += HandleDamageApplied;
            }
        }

        /// <summary>注册一个敌人的 ECS 视觉绑定。</summary>
        /// <param name="enemy">敌人宿主。</param>
        /// <param name="bridge">敌人的 ECS 桥接。</param>
        /// <param name="presentation">生命周期绑定组件。</param>
        internal void Register(
            Enemy enemy,
            EnemyFlockBridge bridge,
            EnemyDamagePresentationComponent presentation)
        {
            if (_disposed || enemy == null || bridge == null || presentation == null)
            {
                return;
            }

            if (_entries.TryGetValue(enemy, out Entry existing))
            {
                existing.Bridge = bridge;
                existing.Presentation = presentation;
                return;
            }

            EnemyVisualConfigSO config = enemy.Attributes != null
                ? enemy.Attributes.visualConfig
                : null;
            Entry entry = new()
            {
                Enemy = enemy,
                Bridge = bridge,
                Presentation = presentation,
                FlashDuration = ResolveFlashDuration(presentation, config),
                DeathDuration = ResolveDeathDuration(presentation, config),
                State = new EnemyFlockVisualState
                {
                    Health01 = 0f,
                    CrackSeed = CreateSeed(enemy),
                    HitFlash = 0f,
                    DeathProgress = 0f,
                    BaseColor = bridge.CachedBaseColor,
                    CrackColor = bridge.CachedCrackColor,
                    EmissionColor = bridge.CachedEmissionColor
                },
                IsDirty = true
            };
            _entries.Add(enemy, entry);

            EnemyHealthComponent health = enemy.GetComponent<EnemyHealthComponent>();
            if (health != null)
            {
                R3.Observable<float> healthStream = health.Health01;
                entry.HealthSubscription = R3.ObservableSubscribeExtensions.Subscribe(
                    healthStream,
                    health01 => SetHealth01(enemy, health01));
            }
        }

        /// <summary>注销一个敌人的视觉绑定并释放集中系统引用。</summary>
        internal void Unregister(Enemy enemy)
        {
            if (enemy != null && _entries.Remove(enemy, out Entry entry))
            {
                entry.HealthSubscription?.Dispose();
                entry.HealthSubscription = null;
            }
        }

        /// <summary>重置对象池实例上的所有视觉状态。</summary>
        internal void Reset(Enemy enemy)
        {
            if (!_entries.TryGetValue(enemy, out Entry entry))
            {
                return;
            }

            EnemyVisualConfigSO config = enemy.Attributes != null
                ? enemy.Attributes.visualConfig
                : null;
            entry.FlashDuration = ResolveFlashDuration(entry.Presentation, config);
            entry.DeathDuration = ResolveDeathDuration(entry.Presentation, config);
            entry.IsDying = false;
            entry.IsInitialized = false;
            entry.State.HitFlash = 0f;
            entry.State.DeathProgress = 0f;
            entry.State.CrackSeed = CreateSeed(enemy);
            entry.IsDirty = true;
            entry.Bridge.SetMotionMode(EnemyFlockMotionMode.Flocking);
        }

        /// <summary>开始一个敌人的死亡视觉流程，但不释放 ECS 实体。</summary>
        internal void BeginDeath(Enemy enemy)
        {
            if (!_entries.TryGetValue(enemy, out Entry entry) || entry.IsDying)
            {
                return;
            }

            entry.IsDying = true;
            entry.State.DeathProgress = 0f;
            entry.IsDirty = true;
            entry.Enemy.SetDamagePresentationState(
                entry.Presentation.DeathHighlightColor,
                entry.DeathDuration);
            entry.Bridge.SetMotionMode(EnemyFlockMotionMode.Frozen);

            if (enemy.Attributes != null)
            {
                _effectService?.Play(enemy.Attributes.deathEffect, enemy.transform.position);
            }
        }

        /// <summary>集中推进所有活动中的闪白和死亡溶解状态。</summary>
        public void Tick()
        {
            if (_disposed || _entries.Count == 0)
            {
                return;
            }

            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            _iterationBuffer.Clear();
            foreach (Entry entry in _entries.Values)
            {
                _iterationBuffer.Add(entry);
            }

            for (int index = 0; index < _iterationBuffer.Count; index++)
            {
                Entry entry = _iterationBuffer[index];
                if (entry == null || entry.Enemy == null || !entry.Bridge.IsEcsControlled)
                {
                    continue;
                }

                if (!entry.IsInitialized)
                {
                    InitializeBoundState(entry);
                }

                bool changed = false;
                if (entry.State.HitFlash > 0f)
                {
                    entry.State.HitFlash = ReduceValue(
                        entry.State.HitFlash,
                        deltaTime,
                        entry.FlashDuration,
                        out bool hitFlashChanged);
                    changed |= hitFlashChanged;
                }

                if (entry.IsDying && entry.State.DeathProgress < 1f)
                {
                    entry.State.DeathProgress = IncreaseValue(
                        entry.State.DeathProgress,
                        deltaTime,
                        entry.DeathDuration,
                        out bool deathChanged);
                    changed |= deathChanged;
                }

                if (changed || entry.IsDirty)
                {
                    entry.Bridge.SetVisualState(entry.State);
                    entry.IsDirty = false;
                }
            }

            _iterationBuffer.Clear();
        }

        /// <summary>注销全局事件订阅并清理敌人引用。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_damageEventSource != null)
            {
                _damageEventSource.DamageApplied -= HandleDamageApplied;
            }

            foreach (Entry entry in _entries.Values)
            {
                entry.HealthSubscription?.Dispose();
                entry.HealthSubscription = null;
            }

            _entries.Clear();
            _iterationBuffer.Clear();
            _disposed = true;
        }

        /// <summary>把实际扣血事件转换为闪白状态和命中特效。</summary>
        private void HandleDamageApplied(DamageEvent damageEvent)
        {
            if (damageEvent.Target is not Enemy enemy ||
                !_entries.TryGetValue(enemy, out Entry entry) ||
                entry.Enemy.IsDead && !damageEvent.WasKilled)
            {
                return;
            }

            entry.State.HitFlash = 1f;
            entry.IsDirty = true;
            enemy.SetDamagePresentationState(
                entry.Presentation.DamageFlashColor,
                entry.FlashDuration);

            if (enemy.Attributes != null)
            {
                _effectService?.Play(
                    enemy.Attributes.hitEffect,
                    damageEvent.HitPosition);
            }
        }

        /// <summary>第一次绑定 ECS 实体时写入配置颜色；生命状态由 R3 订阅提供。</summary>
        private static void InitializeBoundState(Entry entry)
        {
            EnemyVisualConfigSO config = entry.Enemy.Attributes != null
                ? entry.Enemy.Attributes.visualConfig
                : null;
            if (config != null)
            {
                entry.State.BaseColor = ToLinear(config.baseColor);
                entry.State.CrackColor = ToLinear(config.crackColor);
                entry.State.EmissionColor = ToLinear(config.emissionColor);
            }
            else
            {
                entry.State.BaseColor = entry.Bridge.CachedBaseColor;
                entry.State.CrackColor = entry.Bridge.CachedCrackColor;
                entry.State.EmissionColor = entry.Bridge.CachedEmissionColor;
            }

            entry.IsInitialized = true;
            entry.IsDirty = true;
        }

        /// <summary>接收单个生命组件的 R3 百分比状态。</summary>
        private void SetHealth01(Enemy enemy, float health01)
        {
            if (!_entries.TryGetValue(enemy, out Entry entry))
            {
                return;
            }

            float normalizedHealth = Mathf.Clamp01(health01);
            if (Mathf.Approximately(entry.State.Health01, normalizedHealth))
            {
                return;
            }

            entry.State.Health01 = normalizedHealth;
            entry.IsDirty = true;
        }

        /// <summary>按配置解析闪白时长。</summary>
        private static float ResolveFlashDuration(
            EnemyDamagePresentationComponent presentation,
            EnemyVisualConfigSO config)
        {
            return config != null
                ? Mathf.Max(0f, config.damageFlashDuration)
                : presentation != null ? presentation.DamageFlashDuration : 0.08f;
        }

        /// <summary>按配置解析死亡溶解时长。</summary>
        private static float ResolveDeathDuration(
            EnemyDamagePresentationComponent presentation,
            EnemyVisualConfigSO config)
        {
            return config != null
                ? Mathf.Max(0f, config.deathDuration)
                : presentation != null ? presentation.DeathHighlightDuration : 0.18f;
        }

        /// <summary>降低一个有界动画值。</summary>
        private static float ReduceValue(
            float value,
            float deltaTime,
            float duration,
            out bool changed)
        {
            if (duration <= 0f)
            {
                changed = value > 0f;
                return 0f;
            }

            float next = Mathf.Clamp01(value - deltaTime / duration);
            changed = !Mathf.Approximately(value, next);
            return next;
        }

        /// <summary>增加一个有界动画值。</summary>
        private static float IncreaseValue(
            float value,
            float deltaTime,
            float duration,
            out bool changed)
        {
            if (duration <= 0f)
            {
                changed = value < 1f;
                return 1f;
            }

            float next = Mathf.Clamp01(value + deltaTime / duration);
            changed = !Mathf.Approximately(value, next);
            return next;
        }

        /// <summary>根据实例标识生成稳定裂纹种子。</summary>
        private static float CreateSeed(Enemy enemy)
        {
            unchecked
            {
                uint value = (uint)(enemy.GetInstanceID() * 747796405);
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777215f;
            }
        }

        /// <summary>把 Inspector 颜色转换为线性实例颜色。</summary>
        private static float4 ToLinear(Color color)
        {
            Color linear = color.linear;
            return new float4(linear.r, linear.g, linear.b, linear.a);
        }
    }
}
