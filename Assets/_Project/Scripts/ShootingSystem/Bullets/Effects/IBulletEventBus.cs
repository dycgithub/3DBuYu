using System;
using Unity.Mathematics;

namespace ShootingSystem.Bullets.Effects
{
    public interface IBulletEventBus
    {
        event Action<BulletHitEvent> OnHit;
        event Action<BulletExpiredEvent> OnExpired;
        event Action<BulletSpawnedEvent> OnSpawned;

        void EmitHit(int targetInstanceId, float3 hitPoint, float damage, int profileId);
        void EmitExpired(int entityIndex, int profileId);
        void EmitSpawned(BulletSpawnedEvent evt);
    }
}
