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

        private readonly Dictionary<string, GameObject> _effectPrefabs = new();
        private readonly Dictionary<string, List<GameObject>> _activeEffectsByName = new();
        private readonly Dictionary<GameObject, Coroutine> _returnRoutines = new();

        private IGameObjectPool _pool;

        [Inject]
        public void Construct(IGameObjectPool pool)
        {
            _pool = pool;
        }

        public void RegisterEffect(string name, GameObject prefab)
        {
            if (string.IsNullOrWhiteSpace(name) || prefab == null)
                return;

            _effectPrefabs.TryAdd(name, prefab);
        }

        public GameObject PlayEffect(string name, Vector3 position)
        {
            if (!_effectPrefabs.TryGetValue(name, out GameObject prefab) || prefab == null)
            {
                Debug.LogWarning($"Effect '{name}' not found!");
                return null;
            }

            GameObject effect = Play(
                prefab,
                position,
                Quaternion.identity,
                Vector3.one,
                ResolveLifetime(prefab));

            TrackNamedEffect(name, effect);
            return effect;
        }

        public GameObject PlayEffect(string name, Transform parent)
        {
            if (parent == null)
                return PlayEffect(name, Vector3.zero);

            if (!_effectPrefabs.TryGetValue(name, out GameObject prefab) || prefab == null)
            {
                Debug.LogWarning($"Effect '{name}' not found!");
                return null;
            }

            GameObject effect = Play(
                prefab,
                parent.position,
                Quaternion.identity,
                Vector3.one,
                ResolveLifetime(prefab),
                parent);

            TrackNamedEffect(name, effect);
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
            GameObject effect = _pool.Rent(prefab, settings, parent);
            if (effect == null)
                return null;

            Transform effectTransform = effect.transform;
            effectTransform.SetPositionAndRotation(position, rotation);
            effectTransform.localScale = scale;
            RestartParticleSystems(effect);

            if (lifetime > 0f)
                _returnRoutines[effect] = StartCoroutine(ReturnAfterLifetime(effect, lifetime));

            return effect;
        }

        public void Stop(GameObject instance)
        {
            if (instance == null)
                return;

            if (_returnRoutines.Remove(instance, out Coroutine routine))
                StopCoroutine(routine);

            StopParticleSystems(instance);
            RemoveFromNamedTracking(instance);
            _pool?.Return(instance);
        }

        void IEffectService.Play(string effectName, Vector3 position)
        {
            PlayEffect(effectName, position);
        }

        void IEffectService.Stop(string effectName)
        {
            if (!_activeEffectsByName.TryGetValue(effectName, out List<GameObject> effects))
                return;

            for (int index = effects.Count - 1; index >= 0; index--)
                Stop(effects[index]);

            _activeEffectsByName.Remove(effectName);
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

        private void TrackNamedEffect(string name, GameObject effect)
        {
            if (effect == null)
                return;

            if (!_activeEffectsByName.TryGetValue(name, out List<GameObject> effects))
            {
                effects = new List<GameObject>();
                _activeEffectsByName.Add(name, effects);
            }

            effects.Add(effect);
        }

        private void RemoveFromNamedTracking(GameObject effect)
        {
            foreach (List<GameObject> effects in _activeEffectsByName.Values)
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
        }

        private void OnDestroy()
        {
            foreach (Coroutine routine in _returnRoutines.Values)
                StopCoroutine(routine);

            _returnRoutines.Clear();
            _activeEffectsByName.Clear();
        }
    }
}
