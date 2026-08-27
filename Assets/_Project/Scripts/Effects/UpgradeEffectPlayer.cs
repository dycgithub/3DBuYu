using GameSystem;
using Services;
using UnityEngine;

namespace EffectSystem
{
    /// <summary>执行升级配置，运行时依赖由场景容器提供。</summary>
    public sealed class UpgradeEffectPlayer
    {
        private readonly IPooledEffectService _effectService;
        private readonly AudioManager _audioManager;

        public UpgradeEffectPlayer(IPooledEffectService effectService, AudioManager audioManager)
        {
            _effectService = effectService;
            _audioManager = audioManager;
        }

        public GameObject PlaySuccess(UpgradeEffectConfig config, Vector3 position)
        {
            if (config == null)
                return null;

            GameObject effect = PlayVisual(
                config.successEffectPrefab != null ? config.successEffectPrefab : config.effectPrefab,
                config,
                position);
            _audioManager?.PlaySFXAtPosition(config.successSound, position, config.soundVolume);
            return effect;
        }

        public GameObject PlayFailure(UpgradeEffectConfig config, Vector3 position)
        {
            if (config == null)
                return null;

            GameObject effect = PlayVisual(config.failedEffectPrefab, config, position);
            _audioManager?.PlaySFXAtPosition(config.failedSound, position, config.soundVolume);
            return effect;
        }

        public GameObject Play(UpgradeEffectConfig config, Vector3 position)
        {
            return config == null ? null : PlayVisual(config.effectPrefab, config, position);
        }

        private GameObject PlayVisual(GameObject prefab, UpgradeEffectConfig config, Vector3 position)
        {
            if (prefab == null)
                return null;

            return _effectService.Play(
                prefab,
                position + config.effectOffset,
                Quaternion.identity,
                config.effectScale,
                config.loopEffect ? 0f : config.duration);
        }
    }
}
