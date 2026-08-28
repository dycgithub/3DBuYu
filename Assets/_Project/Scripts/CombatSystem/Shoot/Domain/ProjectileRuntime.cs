using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    public sealed class ProjectileRuntime
    {
        public ProjectileInfo Info { get; private set; }
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }
        public float TraveledDistance { get; set; }
        public float RemainingLife { get; set; }
        public int RemainingPenetration { get; set; }
        public GameObject View { get; private set; }
        public bool IsComplete { get; set; }
        public readonly HashSet<int> HitTargetIds = new();

        public ProjectileRuntime(ProjectileInfo info, GameObject view)
        {
            Initialize(info, view);
        }

        public void Initialize(ProjectileInfo info, GameObject view)
        {
            Info = info;
            Position = info.Origin;
            Direction = info.Direction;
            TraveledDistance = 0f;
            RemainingLife = ResolveLifetime(info);
            RemainingPenetration = Mathf.Max(0, info.Penetration);
            View = view;
            IsComplete = false;
            HitTargetIds.Clear();
        }

        public void Reset()
        {
            Info = default;
            Position = default;
            Direction = default;
            TraveledDistance = 0f;
            RemainingLife = 0f;
            RemainingPenetration = 0;
            View = null;
            IsComplete = true;
            HitTargetIds.Clear();
        }

        private static float ResolveLifetime(ProjectileInfo info)
        {
            if (info.Speed <= 0f || info.MaxDistance <= 0f)
                return 0.01f;
            return info.MaxDistance / info.Speed;
        }
    }
}
