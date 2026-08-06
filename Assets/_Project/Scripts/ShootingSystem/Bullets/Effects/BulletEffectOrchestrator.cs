using System.Collections;
using UnityEngine;
using Unity.Mathematics;

namespace ShootingSystem.Bullets.Effects
{
    public class BulletEffectOrchestrator
    {
        private IBulletEventBus _eventBus;
        private MonoBehaviour _coroutineHost;

        public BulletEffectOrchestrator(IBulletEventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.OnHit += OnHit;
            _eventBus.OnExpired += OnExpired;
        }

        public void SetCoroutineHost(MonoBehaviour host)
        {
            _coroutineHost = host;
        }

        private void OnHit(BulletHitEvent evt)
        {
            if (_coroutineHost == null) return;

            _coroutineHost.StartCoroutine(DelayedInvoke(0.1f, () =>
            {
                var profile = Resources.EntityIdToObject(evt.ProfileId) as BulletProfile;
                var ctx = new BulletEffectContext
                {
                    TargetInstanceId = evt.TargetInstanceId,
                    HitPoint = evt.HitPoint,
                    Damage = evt.Damage,
                    Profile = profile
                };
                ProcessHitEffects(profile, ctx);
            }));
        }

        private void OnExpired(BulletExpiredEvent evt)
        {
            if (_coroutineHost == null) return;

            _coroutineHost.StartCoroutine(DelayedInvoke(0.1f, () =>
            {
                var profile = Resources.EntityIdToObject(evt.ProfileId) as BulletProfile;
                var ctx = new BulletEffectContext
                {
                    TargetInstanceId = 0,
                    HitPoint = float3.zero,
                    Damage = 0f,
                    Profile = profile
                };
                ProcessExpiredEffects(profile, ctx);
            }));
        }

        public void ProcessHitEffects(BulletProfile profile, BulletEffectContext context)
        {
            if (profile == null) return;
            if (profile.OnHitEffects != null)
            {
                foreach (var effect in profile.OnHitEffects)
                {
                    if (effect != null)
                        effect.Execute(context);
                }
            }
        }

        public void ProcessExpiredEffects(BulletProfile profile, BulletEffectContext context)
        {
            if (profile == null) return;
            if (profile.OnExpiredEffects != null)
            {
                foreach (var effect in profile.OnExpiredEffects)
                {
                    if (effect != null)
                        effect.Execute(context);
                }
            }
        }

        private static IEnumerator DelayedInvoke(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }
    }
}
