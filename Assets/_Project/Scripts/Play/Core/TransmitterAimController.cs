using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace Play
{
    /// <summary>
    /// 端口朝向控制器。
    /// 端口集合和依赖在初始化时缓存；运行中只读取探测器目标和球面中心。
    /// </summary>
    public class TransmitterAimController : MonoBehaviour
    {
        [Header("依赖")]
        [SerializeField] private SphereWalker _sphereWalker;
        [SerializeField] private PhysicsCentralDetector _detector;

        private IReadOnlyList<CentralTransmitterRuntime> _ports;
        private bool _initialized;

        public void Initialize(
            IReadOnlyList<CentralTransmitterRuntime> ports,
            PhysicsCentralDetector targetDetector,
            SphereWalker walker)
        {
            _ports = ports;
            _detector = targetDetector != null ? targetDetector : _detector;
            _sphereWalker = walker != null ? walker : _sphereWalker;
            _initialized = _ports != null;
        }

        private void Awake()
        {
            if (_sphereWalker == null)
                _sphereWalker = GetComponentInParent<SphereWalker>();
            if (_detector == null)
                _detector = GetComponent<PhysicsCentralDetector>();
        }

        private void Update()
        {
            if (!_initialized || _detector == null || _ports == null)
                return;

            Vector3 sphereCenter = _sphereWalker != null
                ? _sphereWalker.GetEffectiveCenter()
                : Vector3.zero;

            for (int i = 0; i < _ports.Count; i++)
            {
                CentralTransmitterRuntime port = _ports[i];
                if (port == null || port.FirePoint == null || port.IsLocked)
                    continue;

                IDamageable target = _detector.GetTarget(i);
                if (target != null && target.IsAlive)
                    AimAtTarget(port, target);
                else
                    ReturnToSphereCenter(port, sphereCenter);
            }
        }

        private void AimAtTarget(CentralTransmitterRuntime port, IDamageable target)
        {
            Transform firePoint = port.FirePoint;
            Vector3 toTarget = target.Position - firePoint.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                return;

            // 保持原有行为：端口跟踪速度来自静态端口配置，而不是属性缩放值。
            float trackingSpeed = port.So.trackingSpeed;
            Quaternion desired = Quaternion.LookRotation(toTarget.normalized);
            firePoint.rotation = Quaternion.RotateTowards(
                firePoint.rotation,
                desired,
                trackingSpeed * Time.deltaTime);
        }

        private static void ReturnToSphereCenter(CentralTransmitterRuntime port, Vector3 sphereCenter)
        {
            Transform firePoint = port.FirePoint;
            Vector3 direction = sphereCenter - firePoint.position;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            firePoint.rotation = Quaternion.LookRotation(direction.normalized);
        }

#if UNITY_EDITOR
        [Header("调试可视化")]
        public bool showTargetLines = true;
        public Color targetLineColor = Color.red;

        private void OnDrawGizmosSelected()
        {
            if (!showTargetLines || !_initialized || _detector == null || _ports == null)
                return;
            if (!Application.isPlaying)
                return;

            Gizmos.color = targetLineColor;
            for (int i = 0; i < _ports.Count; i++)
            {
                CentralTransmitterRuntime port = _ports[i];
                if (port == null || port.FirePoint == null)
                    continue;

                IDamageable target = _detector.GetTarget(i);
                if (target != null && target.IsAlive)
                    Gizmos.DrawLine(port.FirePoint.position, target.Position);
            }
        }
#endif
    }
}
