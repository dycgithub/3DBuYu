using System.Collections;
using System.Collections.Generic;
using Services;
using UnityEngine;
using Utils;
using VContainer;

namespace EffectSystem
{
    public sealed class EffectManager : MonoBehaviour, IEffectService, IPooledEffectService
    {
        [SerializeField, Min(0)] private int _prewarmCount;
        [SerializeField, Min(1)] private int _maximumRetainedPerPrefab = 64;
        [SerializeField, Min(0.01f)] private float _fallbackLifetime = 2f;

        private readonly Dictionary<EffectId, List<GameObject>> _activeEffectsById = new();
        private readonly Dictionary<GameObject, Coroutine> _returnRoutines = new();
        private readonly HashSet<GameObject> _activeEffects = new();

        private IGameObjectPool _pool;
        private CombatEffectCatalogSO _catalog;

        /// <summary>注入对象池并尝试获取特效目录；目录缺失时保留管理器但不播放目录特效。</summary>
        /// <param name="pool">提供特效实例租借和归还能力的对象池。</param>
        /// <param name="resolver">当前 GameLoop 容器，用于可选解析特效目录。</param>
        [Inject]
        public void Construct(IGameObjectPool pool, IObjectResolver resolver)
        {
            _pool = pool;
            _catalog = resolver.TryResolve(out CombatEffectCatalogSO catalog)
                ? catalog
                : null;
        }

        public GameObject PlayEffect(EffectId effectId, Vector3 position)
        {
            return PlayEffect(effectId, position, null);
        }

        public GameObject PlayEffect(EffectId effectId, Transform parent)
        {
            return PlayEffect(effectId, parent != null ? parent.position : Vector3.zero, parent);
        }

        private GameObject PlayEffect(EffectId effectId, Vector3 position, Transform parent)
        {
            if (effectId == EffectId.None || _catalog == null || !_catalog.TryGet(effectId, out EffectCatalogEntry entry) || entry.Prefab == null)
            {
                Debug.LogWarning($"Effect '{effectId}' not found in the combat effect catalog.");
                return null;
            }

            float lifetime = entry.Lifetime > 0f
                ? entry.Lifetime
                : ResolveLifetime(entry.Prefab);
            PoolSettings settings = new(entry.PrewarmCount, Mathf.Max(1, entry.MaximumRetained));
            GameObject effect = PlayPooled(
                entry.Prefab,
                position,
                Quaternion.identity,
                Vector3.one * Mathf.Max(0.01f, entry.Scale),
                lifetime,
                parent,
                settings);

            TrackNamedEffect(effectId, effect);
            return effect;
        }

        public GameObject Play(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float lifetime,
            Transform parent = null)
        {
            if (prefab == null || _pool == null)
                return null;

            PoolSettings settings = new(_prewarmCount, _maximumRetainedPerPrefab);
            return PlayPooled(prefab, position, rotation, scale, lifetime, parent, settings);
        }

        private GameObject PlayPooled(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float lifetime,
            Transform parent,
            PoolSettings settings)
        {
            if (prefab == null || _pool == null)
                return null;

            GameObject effect = _pool.Rent(prefab, settings, parent);
            if (effect == null)
                return null;

            _activeEffects.Add(effect);
            Transform effectTransform = effect.transform;
            if (parent != null)
            {
                effectTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            else
            {
                effectTransform.SetPositionAndRotation(position, rotation);
            }

            effectTransform.localScale = scale;
            PrepareForPooling(effect);
            RestartParticleSystems(effect);

            if (lifetime > 0f)
                _returnRoutines[effect] = StartCoroutine(ReturnAfterLifetime(effect, lifetime));

            return effect;
        }

        public void Stop(GameObject instance)
        {
            if (ReferenceEquals(instance, null))
                return;

            bool wasActive = _activeEffects.Remove(instance);
            if (_returnRoutines.Remove(instance, out Coroutine routine))
            {
                StopCoroutine(routine);
                wasActive = true;
            }

            RemoveFromNamedTracking(instance);
            if (!wasActive)
            {
                return;
            }

            if (instance == null)
                return;

            StopParticleSystems(instance);
            _pool?.Return(instance);
        }

        void IEffectService.Play(EffectId effectId, Vector3 position)
        {
            PlayEffect(effectId, position);
        }

        void IEffectService.Play(EffectId effectId, Vector3 position, Transform parent)
        {
            PlayEffect(effectId, position, parent);
        }

        void IEffectService.Stop(EffectId effectId)
        {
            if (!_activeEffectsById.TryGetValue(effectId, out List<GameObject> effects))
                return;

            for (int index = effects.Count - 1; index >= 0; index--)
                Stop(effects[index]);

            _activeEffectsById.Remove(effectId);
        }

        private IEnumerator ReturnAfterLifetime(GameObject effect, float lifetime)
        {
            yield return new WaitForSecondsRealtime(lifetime);
            Stop(effect);
        }

        private float ResolveLifetime(GameObject prefab)
        {
            ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            float longestLifetime = 0f;

            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.MainModule main = system.main;
                if (main.loop)
                    return 0f;

                longestLifetime = Mathf.Max(
                    longestLifetime,
                    main.duration + main.startLifetime.constantMax);
            }

            return longestLifetime > 0f ? longestLifetime : _fallbackLifetime;
        }

        private void TrackNamedEffect(EffectId effectId, GameObject effect)
        {
            if (effect == null)
                return;

            if (!_activeEffectsById.TryGetValue(effectId, out List<GameObject> effects))
            {
                effects = new List<GameObject>();
                _activeEffectsById.Add(effectId, effects);
            }

            effects.Add(effect);
        }

        private void RemoveFromNamedTracking(GameObject effect)
        {
            foreach (List<GameObject> effects in _activeEffectsById.Values)
                effects.Remove(effect);
        }

        private static void RestartParticleSystems(GameObject effect)
        {
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Clear(true);
                system.Play(true);
            }
        }

        private static void StopParticleSystems(GameObject effect)
        {
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            foreach (TrailRenderer trail in effect.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.emitting = false;
                trail.Clear();
            }
        }

        private static void PrepareForPooling(GameObject effect)
        {
            // CFXR 默认会 Destroy 自身；对象池中的实例必须改为 Disable。
            foreach (CartoonFX.CFXR_Effect cfxrEffect in effect.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true))
                cfxrEffect.clearBehavior = CartoonFX.CFXR_Effect.ClearBehavior.Disable;

            foreach (TrailRenderer trail in effect.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.emitting = true;
                trail.Clear();
            }
        }

        /// <summary>管理器被禁用或场景卸载时归还全部活动特效。</summary>
        private void OnDisable()
        {
            ClearActiveEffects();
        }

        private void OnDestroy()
        {
            ClearActiveEffects();
            _returnRoutines.Clear();
            _activeEffects.Clear();
            _activeEffectsById.Clear();
        }

        /// <summary>在管理器销毁前归还所有仍由本组件租借的特效。</summary>
        private void ClearActiveEffects()
        {
            var activeEffects = new List<GameObject>(_activeEffects);
            for (int index = 0; index < activeEffects.Count; index++)
            {
                Stop(activeEffects[index]);
            }
        }
    }
}
