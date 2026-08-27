using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace Play
{
    public enum TargetingMode
    {
        Nearest,
        Distributed,
        FocusFire
    }

    /// <summary>
    /// 中心炮台的物理探测与目标分配组件。
    /// 探测结果以端口索引保存，控制器只读取目标，不参与物理查询和分配过程。
    /// </summary>
    public class PhysicsCentralDetector : MonoBehaviour
    {
        [Header("探测")]
        [SerializeField] public float detectionRadius = 15f;
        [SerializeField] public LayerMask enemyLayerMask = -1;

        [Header("目标选择")]
        [SerializeField] public TargetingMode targetingMode = TargetingMode.Distributed;

        [Header("缓冲区")]
        [SerializeField, Min(16)] private int initialBufferCapacity = 256;

        private IDamageable[] _targets = System.Array.Empty<IDamageable>();
        private Collider[] _detectionBuffer;
        private readonly List<IDamageable> _cached = new List<IDamageable>(128);
        private readonly List<IDamageable> _sortedTargets = new List<IDamageable>(128);
        private readonly HashSet<IDamageable> _uniqueTargets = new HashSet<IDamageable>();
        private CentralAttributes _attributes;

        public TargetingMode TargetingMode
        {
            get => targetingMode;
            set => targetingMode = value;
        }

        public int PortCount => _targets.Length;

        private float EffectiveDetectionRadius
            => _attributes != null ? _attributes.DetectionRadius : Mathf.Max(0f, detectionRadius);

        private void Awake()
        {
            EnsureDetectionBuffer();
            UseEnemyLayerWhenAvailable();
        }

        /// <summary>由中心炮台一次性绑定运行时属性和端口数量。</summary>
        public void Initialize(int portCount, CentralAttributes attributes)
        {
            BindAttributes(attributes);
            SetPortCount(portCount);
            EnsureDetectionBuffer();
            UseEnemyLayerWhenAvailable();
        }

        public void BindAttributes(CentralAttributes attributes)
        {
            _attributes = attributes;
        }

        public void SetPortCount(int count)
        {
            count = Mathf.Max(0, count);
            if (_targets.Length != count)
                _targets = new IDamageable[count];
        }

        public bool HasTarget(int index)
        {
            return IsValidIndex(index) && _targets[index] != null && _targets[index].IsAlive;
        }

        public IDamageable GetTarget(int index)
        {
            return IsValidIndex(index) ? _targets[index] : null;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < _targets.Length;
        }

        private void Update()
        {
            DetectEnemies();
            AssignTargets();
        }

        private void DetectEnemies()
        {
            _cached.Clear();
            _uniqueTargets.Clear();

            int hitCount = QueryColliders();
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = _detectionBuffer[i];
                if (collider == null)
                    continue;

                IDamageable damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive && _uniqueTargets.Add(damageable))
                    _cached.Add(damageable);
            }
        }

        private int QueryColliders()
        {
            EnsureDetectionBuffer();

            while (true)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(
                    transform.position,
                    EffectiveDetectionRadius,
                    _detectionBuffer,
                    enemyLayerMask);

                // 返回容量表示缓冲区可能被填满，扩大后重新查询，避免静默丢失敌人。
                if (hitCount < _detectionBuffer.Length)
                    return hitCount;

                int nextCapacity = Mathf.Max(_detectionBuffer.Length * 2, _detectionBuffer.Length + 1);
                System.Array.Resize(ref _detectionBuffer, nextCapacity);
            }
        }

        private void EnsureDetectionBuffer()
        {
            int capacity = Mathf.Max(16, initialBufferCapacity);
            if (_detectionBuffer == null || _detectionBuffer.Length == 0)
                _detectionBuffer = new Collider[capacity];
        }

        private void UseEnemyLayerWhenAvailable()
        {
            // -1 表示未指定过滤层。Enemy 层存在时使用它，否则保留全层查询，避免把 -1 左移造成错误掩码。
            if (enemyLayerMask.value != -1)
                return;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                enemyLayerMask = 1 << enemyLayer;
        }

        private void AssignTargets()
        {
            switch (targetingMode)
            {
                case TargetingMode.Nearest:
                    AssignNearest();
                    break;
                case TargetingMode.Distributed:
                    AssignDistributed();
                    break;
                case TargetingMode.FocusFire:
                    AssignFocusFire();
                    break;
            }
        }

        private void AssignNearest()
        {
            IDamageable nearest = FindNearest();
            for (int i = 0; i < _targets.Length; i++)
                _targets[i] = nearest;
        }

        private void AssignDistributed()
        {
            _sortedTargets.Clear();
            _sortedTargets.AddRange(_cached);
            Vector3 origin = transform.position;
            _sortedTargets.Sort((left, right) =>
                SqrDistance(origin, left.Position).CompareTo(SqrDistance(origin, right.Position)));

            for (int i = 0; i < _targets.Length; i++)
            {
                if (_sortedTargets.Count == 0)
                    _targets[i] = null;
                else
                    _targets[i] = _sortedTargets[Mathf.Min(i, _sortedTargets.Count - 1)];
            }
        }

        private void AssignFocusFire()
        {
            IDamageable primary = null;
            float highestThreat = float.MinValue;

            for (int i = 0; i < _cached.Count; i++)
            {
                IDamageable target = _cached[i];
                if (target == null || !target.IsAlive)
                    continue;

                float threat = target is ILockable lockable ? lockable.ThreatLevel : 0f;
                if (primary == null || threat > highestThreat)
                {
                    highestThreat = threat;
                    primary = target;
                }
            }

            if (primary == null)
                primary = FindNearest();

            for (int i = 0; i < _targets.Length; i++)
                _targets[i] = primary;
        }

        private IDamageable FindNearest()
        {
            IDamageable nearest = null;
            float nearestDistance = float.MaxValue;
            Vector3 origin = transform.position;

            for (int i = 0; i < _cached.Count; i++)
            {
                IDamageable target = _cached[i];
                if (target == null || !target.IsAlive)
                    continue;

                float distance = SqrDistance(origin, target.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = target;
                }
            }

            return nearest;
        }

        private static float SqrDistance(Vector3 origin, Vector3 targetPosition)
        {
            return (targetPosition - origin).sqrMagnitude;
        }

#if UNITY_EDITOR
        [Header("调试可视化")]
        public bool showDetectionGizmos = true;
        public Color detectionSphereColor = new Color(0f, 0.7f, 1f, 0.12f);

        private void OnDrawGizmosSelected()
        {
            if (!showDetectionGizmos)
                return;

            Gizmos.color = detectionSphereColor;
            Gizmos.DrawSphere(transform.position, EffectiveDetectionRadius);
            Gizmos.color = new Color(
                detectionSphereColor.r,
                detectionSphereColor.g,
                detectionSphereColor.b,
                0.6f);
            Gizmos.DrawWireSphere(transform.position, EffectiveDetectionRadius);
        }
#endif
    }
}
