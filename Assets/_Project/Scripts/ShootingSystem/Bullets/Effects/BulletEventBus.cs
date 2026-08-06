using System;
using Unity.Mathematics;

namespace ShootingSystem.Bullets.Effects
{
    public class BulletEventBus : IBulletEventBus
    {
        public event Action<BulletHitEvent> OnHit;
        public event Action<BulletExpiredEvent> OnExpired;
        public event Action<BulletSpawnedEvent> OnSpawned;

        public void EmitHit(int targetInstanceId, float3 hitPoint, float damage, int profileId)
        {
            OnHit?.Invoke(new BulletHitEvent
            {
                TargetInstanceId = targetInstanceId,
                HitPoint = hitPoint,
                Damage = damage,
                ProfileId = profileId
            });
        }

        public void EmitExpired(int entityIndex, int profileId)
        {
            OnExpired?.Invoke(new BulletExpiredEvent { EntityIndex = entityIndex, ProfileId = profileId });
        }

        public void EmitSpawned(BulletSpawnedEvent evt)
        {
            OnSpawned?.Invoke(evt);
        }
    }
}
