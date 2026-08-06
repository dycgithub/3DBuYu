using UnityEngine;
using Interfaces;

namespace TurretSystem
{
    /// <summary>
    /// 端口朝向策略控制器。
    /// 每帧根据探测结果决定每个 port 的朝向:
    ///   - 无目标 → 指向 SphereWalker.GetEffectiveCenter()
    ///   - 有目标 → 指向分配到的敌人
    /// </summary>
    [RequireComponent(typeof(Turret))]
    public class PortAimController : MonoBehaviour
    {
        [Header("依赖")]
        [SerializeField] private SphereWalker _sphereWalker;
        [SerializeField] private Turret _turret;
        [SerializeField] private PhysicsTurretDetector _detector;

        private void Awake()
        {
            if (_sphereWalker == null) _sphereWalker = GetComponentInParent<SphereWalker>();
            if (_turret == null) _turret = GetComponent<Turret>();
            if (_detector == null) _detector = GetComponent<PhysicsTurretDetector>();
        }

        private void Update()
        {
            if (_turret == null || _detector == null) return;

            int portCount = _turret.PortCount;
            Vector3 sphereCenter = _sphereWalker != null
                ? _sphereWalker.GetEffectiveCenter()
                : Vector3.zero;

            for (int i = 0; i < portCount; i++)
            {
                var port = _turret.GetPort(i);
                if (port == null || port.FirePoint == null || port.IsLocked) continue;

                IDamageable target = _detector.GetTarget(i);
                if (target != null && target.IsAlive)
                    AimAtTarget(port, target);
                else
                    ReturnToSphereCenter(port, sphereCenter);
            }
        }

        private void AimAtTarget(TurretPortRuntime port, IDamageable target)
        {
            Transform fp = port.FirePoint;
            Vector3 toTarget = (target.Position - fp.position).normalized;
            Quaternion desired = Quaternion.LookRotation(toTarget);
            fp.rotation = Quaternion.RotateTowards(
                fp.rotation, desired, port.Config.trackingSpeed * Time.deltaTime);
        }

        private void ReturnToSphereCenter(TurretPortRuntime port, Vector3 sphereCenter)
        {
            Transform fp = port.FirePoint;
            Vector3 dir = (sphereCenter - fp.position).normalized;
            fp.rotation = Quaternion.LookRotation(dir);
        }

#if UNITY_EDITOR
        [Header("调试可视化")]
        public bool showTargetLines = true;
        public Color targetLineColor = Color.red;

        private void OnDrawGizmosSelected()
        {
            if (!showTargetLines || _turret == null || _detector == null) return;
            if (!Application.isPlaying) return;

            Gizmos.color = targetLineColor;
            for (int i = 0; i < _turret.PortCount; i++)
            {
                var port = _turret.GetPort(i);
                if (port == null || port.FirePoint == null) continue;

                var target = _detector.GetTarget(i);
                if (target != null && target.IsAlive)
                    Gizmos.DrawLine(port.FirePoint.position, target.Position);
            }
        }
#endif
    }
}
