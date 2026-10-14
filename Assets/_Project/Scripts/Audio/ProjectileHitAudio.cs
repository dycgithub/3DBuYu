using System;
using System.Collections.Generic;
using CombatSystem;
using UnityEngine;
using VContainer.Unity;

namespace GameSystem
{
    /// <summary>只为有效投射物命中播放一次空间 impact 音效。</summary>
    public sealed class ProjectileHitAudio : IStartable, IDisposable
    {
        private const float ImpactVolume = 0.35f;

        private readonly IProjectileHitEventSource _hitSource;
        private readonly AudioManager _audioManager;
        private readonly HashSet<HitKey> _playedHitsThisFrame = new();

        private int _trackedFrame = -1;

        /// <summary>创建命中音效监听器。</summary>
        /// <param name="hitSource">发布统一投射物命中结果的来源。</param>
        /// <param name="audioManager">负责音源池与空间音效播放的管理器。</param>
        public ProjectileHitAudio(
            IProjectileHitEventSource hitSource,
            AudioManager audioManager)
        {
            _hitSource = hitSource;
            _audioManager = audioManager;
        }

        /// <summary>开始监听统一投射物命中事件。</summary>
        public void Start()
        {
            _hitSource.HitProcessed += HandleHitProcessed;
        }

        /// <summary>停止监听并清理当前帧的去重状态。</summary>
        public void Dispose()
        {
            _hitSource.HitProcessed -= HandleHitProcessed;
            _playedHitsThisFrame.Clear();
            _trackedFrame = -1;
        }

        /// <summary>仅播放有效伤害，并合并同一攻击在同一帧对同一目标的重复音效。</summary>
        /// <param name="context">命中目标、音效配置和伤害结果。</param>
        private void HandleHitProcessed(ProjectileHitContext context)
        {
            if (!context.DamageResult.WasApplied)
            {
                return;
            }

            ProjectileProfileSO profile = context.ProjectileData.ProjectileProfileSnapshot != null
                ? context.ProjectileData.ProjectileProfileSnapshot
                : context.ProjectileData.ProjectileProfile;
            if (profile == null || profile.ImpactSound == null)
            {
                return;
            }

            int frame = Time.frameCount;
            if (_trackedFrame != frame)
            {
                _trackedFrame = frame;
                _playedHitsThisFrame.Clear();
            }

            int targetInstanceId = context.Target?.Transform != null
                ? context.Target.Transform.GetInstanceID()
                : 0;
            if (targetInstanceId != 0 && !_playedHitsThisFrame.Add(new HitKey(
                    context.ProjectileData.AttackInstanceId,
                    targetInstanceId)))
            {
                return;
            }

            _audioManager.PlaySFXAtPosition(
                profile.ImpactSound,
                context.HitPosition,
                ImpactVolume);
        }

        private readonly struct HitKey : IEquatable<HitKey>
        {
            private readonly int _attackInstanceId;
            private readonly int _targetInstanceId;

            public HitKey(int attackInstanceId, int targetInstanceId)
            {
                _attackInstanceId = attackInstanceId;
                _targetInstanceId = targetInstanceId;
            }

            public bool Equals(HitKey other)
            {
                return _attackInstanceId == other._attackInstanceId
                    && _targetInstanceId == other._targetInstanceId;
            }

            public override bool Equals(object obj)
            {
                return obj is HitKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_attackInstanceId * 397) ^ _targetInstanceId;
                }
            }
        }
    }
}
