using UnityEngine;
using System.Collections.Generic;
using Interfaces;

namespace TurretSystem
{
    public enum TargetingMode
    {
        Nearest,
        Distributed,
        FocusFire
    }

    /// <summary>
    /// 物理炮塔探测器：负责探测敌人并为每个 port 分配目标。
    /// 端口朝向控制已迁移至 PortAimController。
    /// </summary>
    public class PhysicsTurretDetector : MonoBehaviour
    {
        [Header("探测")]
        public float detectionRadius = 15f;
        public LayerMask enemyLayerMask = -1;

        [Header("目标选择")]
        public TargetingMode targetingMode = TargetingMode.Distributed;

        private IDamageable[] _targets;

        private Collider[] _detectionBuffer = new Collider[256];
        private List<IDamageable> _cached = new(128);

        private TurretAttributes _attributes;

        /// <summary>绑定炮塔属性聚合器,探测半径改由装备属性驱动。</summary>
        public void BindAttributes(TurretAttributes attributes) => _attributes = attributes;

        private float EffectiveDetectionRadius
            => _attributes != null ? _attributes.DetectionRadius : detectionRadius;

        public int PortCount => _targets?.Length ?? 0;
        public bool HasTarget(int index) => index < _targets?.Length && _targets[index]?.IsAlive == true;
        public IDamageable GetTarget(int index) => (index < _targets?.Length) ? _targets[index] : null;

        public void SetPortCount(int count)
        {
            if (_targets == null || _targets.Length != count)
                _targets = new IDamageable[count];
        }

        private void Start()
        {
            if (_targets == null)
                _targets = new IDamageable[0];
        }

        private void Update()
        {
            DetectEnemies();
            AssignTargets();
        }

        private void DetectEnemies()
        {
            _cached.Clear();
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position, EffectiveDetectionRadius, _detectionBuffer, enemyLayerMask);
            for (int i = 0; i < hitCount; i++)
            {
                var damageable = _detectionBuffer[i].GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    _cached.Add(damageable);
            }
        }

        private void AssignTargets()
        {
            if (_targets == null) return;

            switch (targetingMode)
            {
                case TargetingMode.Nearest: AssignNearest(); break;
                case TargetingMode.Distributed: AssignDistributed(); break;
                case TargetingMode.FocusFire: AssignFocusFire(); break;
            }
        }

        private void AssignNearest()
        {
            IDamageable nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var t in _cached)
            {
                if (t == null || !t.IsAlive) continue;
                float dist = Vector3.Distance(transform.position, t.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = t;
                }
            }
            for (int i = 0; i < _targets.Length; i++)
                _targets[i] = nearest;
        }

        private void AssignDistributed()
        {
            var sorted = new List<IDamageable>(_cached);
            sorted.Sort((a, b) =>
                Vector3.Distance(transform.position, a.Position)
                .CompareTo(Vector3.Distance(transform.position, b.Position)));

            int idx = 0;
            for (int i = 0; i < _targets.Length; i++)
            {
                if (idx < sorted.Count)
                {
                    _targets[i] = sorted[idx];
                    idx++;
                }
                else
                {
                    _targets[i] = sorted.Count > 0 ? sorted[0] : null;
                }
            }
        }

        private void AssignFocusFire()
        {
            IDamageable primary = null;
            float highestThreat = float.MinValue;

            foreach (var t in _cached)
            {
                if (t == null || !t.IsAlive) continue;
                float threat = (t is ILockable lockable) ? lockable.ThreatLevel : 0f;
                if (threat > highestThreat)
                {
                    highestThreat = threat;
                    primary = t;
                }
            }

            if (primary == null)
            {
                float nearestDist = float.MaxValue;
                foreach (var t in _cached)
                {
                    if (t == null || !t.IsAlive) continue;
                    float dist = Vector3.Distance(transform.position, t.Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        primary = t;
                    }
                }
            }

            for (int i = 0; i < _targets.Length; i++)
                _targets[i] = primary;
        }

#if UNITY_EDITOR
        [Header("调试可视化")]
        public bool showDetectionGizmos = true;
        public Color detectionSphereColor = new Color(0f, 0.7f, 1f, 0.12f);

        private void OnDrawGizmosSelected()
        {
            if (!showDetectionGizmos) return;

            Gizmos.color = detectionSphereColor;
            Gizmos.DrawSphere(transform.position, EffectiveDetectionRadius);
            Gizmos.color = new Color(detectionSphereColor.r, detectionSphereColor.g, detectionSphereColor.b, 0.6f);
            Gizmos.DrawWireSphere(transform.position, EffectiveDetectionRadius);
        }
#endif
    }
}
