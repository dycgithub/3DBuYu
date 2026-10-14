using System.Collections.Generic;
using Interfaces;
using CombatSystem;
using System;
using R3;
using Services;
using UnityEngine;
using VContainer;
using Play.TargetMark;

namespace Play
{
    /// <summary>
    /// 自动和按键模式跟随分配目标；标记模式跟随队首、无目标时回瞄球心，鼠标按住由开火控制器接管。
    /// </summary>
    public class TransmitterAimController : MonoBehaviour
    {
        [Header("依赖")]
        [SerializeField] private SphereWalker _sphereWalker;
        [SerializeField] private PhysicsCentralDetector _detector;

        private IReadOnlyList<CentralTransmitterRuntime> _ports;
        private bool _initialized;
        private bool _mouseFireHeld;
        private IDisposable _mouseFireSubscription;

        [Inject] private ITransmitterGlobalEffectService _globalEffects;
        [Inject] private IInputService _input;
        [Inject] private ITransmitterAttackModeProvider _attackModeProvider;
        [Inject] private MarkedTargetService _markedTarget;

        /// <summary>订阅鼠标持续按住状态，供鼠标模式让出瞄准控制。</summary>
        private void Start()
        {
            if (_input != null)
            {
                _mouseFireSubscription = _input.MouseFireHeld.Subscribe(HandleMouseFireChanged);
            }
        }

        /// <summary>释放鼠标按住状态订阅。</summary>
        private void OnDestroy()
        {
            _mouseFireSubscription?.Dispose();
        }

        /// <summary>记录鼠标当前是否持续按住。</summary>
        /// <param name="isHeld">鼠标左键是否按住。</param>
        private void HandleMouseFireChanged(bool isHeld)
        {
            _mouseFireHeld = isHeld;
        }

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
            {
                _sphereWalker = GetComponentInParent<SphereWalker>();
            }

            if (_detector == null)
            {
                _detector = GetComponent<PhysicsCentralDetector>();
            }
        }

        private void Update()
        {
            if (!_initialized || _detector == null || _ports == null)
            {
                return;
            }

            TransmitterAttackMode attackMode = _attackModeProvider != null
                ? _attackModeProvider.AttackMode
                : TransmitterAttackMode.Automatic;
            if (attackMode == TransmitterAttackMode.Mouse && _mouseFireHeld)
            {
                return;
            }

            Vector3 sphereCenter = _sphereWalker != null
                ? _sphereWalker.GetEffectiveCenter()
                : Vector3.zero;

            for (int i = 0; i < _ports.Count; i++)
            {
                CentralTransmitterRuntime port = _ports[i];
                if (port == null || port.FirePoint == null || port.IsLocked)
                {
                    continue;
                }

                IDamageable target = attackMode == TransmitterAttackMode.MarkedTarget
                    ? _markedTarget?.Target
                    : _detector.GetTarget(i);
                if (attackMode != TransmitterAttackMode.MarkedTarget)
                {
                    IDamageable focusedTarget = _globalEffects?.GetFocusedTarget();
                    if (focusedTarget.IsAliveAndValid())
                    {
                        target = focusedTarget;
                    }
                }

                if (target.IsAliveAndValid())
                {
                    AimAtTarget(port, target);
                }
                else
                {
                    ReturnToSphereCenter(port, sphereCenter);
                }
            }
        }

        private void AimAtTarget(CentralTransmitterRuntime port, IDamageable target)
        {
            Transform firePoint = port.FirePoint;
            Vector3 toTarget = target.Position - firePoint.position;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

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
            {
                return;
            }

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
                if (target.IsAliveAndValid())
                    Gizmos.DrawLine(port.FirePoint.position, target.Position);
            }
        }
#endif
    }
}
