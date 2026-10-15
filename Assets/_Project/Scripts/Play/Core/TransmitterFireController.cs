using System;
using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using R3;
using Services;
using Play.TargetMark;
using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>
    /// 根据已选择的攻击模式提交攻击，并在攻击成功后请求对应的菱形魔方转动。
    /// <para>端口的基础攻击数值和子弹配置全部来自对应的 <see cref="TransmitterSO"/>。</para>
    /// <para>端口转动映射按中央核心配置顺序保存，可在 Inspector 中逐端口修改。</para>
    /// </summary>
    public class TransmitterFireController : MonoBehaviour
    {
        private const int MaxPuzzlePorts = 8;

        private CentralCore _centralCore;
        private PhysicsCentralDetector _detector;
        private DiamondTwistController _diamondTwist;
        private Transform[] _firePoints;
        private bool _initialized;
        private bool _mouseFireHeld;
        private IDisposable _mouseFireSubscription;

        [Header("Diamond puzzle firing mapping")]
        [InspectorName("Port Move Mapping (8 ports)")]
        [Tooltip("CentralBase order: Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left, TopLeft. Defaults: unlocked Top/Right/Bottom/Left use L'/R'/D'/B'; remaining ports use L/R/D/B.")]
        [SerializeField] private DiamondTwistMove[] _puzzleMoves =
        {
            DiamondTwistMove.LPrime,
            DiamondTwistMove.L,
            DiamondTwistMove.RPrime,
            DiamondTwistMove.R,
            DiamondTwistMove.DPrime,
            DiamondTwistMove.D,
            DiamondTwistMove.BPrime,
            DiamondTwistMove.B
        };

        [Inject] private ITransmitterAttackService _attackService;
        [Inject] private ITransmitterGlobalEffectService _globalEffects;
        [Inject] private IInputService _input;
        [Inject] private ITransmitterAttackModeProvider _attackModeProvider;
        [Inject] private MarkedTargetService _markedTarget;
        [Inject] private Camera _camera;

        /// <summary>绑定中央核心、端口集合和每个端口的发射点。</summary>
        /// <param name="core">所属中央核心。</param>
        /// <param name="ports">中央核心中的发射器端口集合。</param>
        /// <param name="targetDetector">提供各端口自动目标的探测器。</param>
        public void Initialize(
            CentralCore core,
            IReadOnlyList<CentralTransmitterRuntime> ports,
            PhysicsCentralDetector targetDetector)
        {
            _centralCore = core;
            _detector = targetDetector;
            _diamondTwist = _centralCore != null
                ? _centralCore.GetComponentInChildren<DiamondTwistController>()
                : null;

            int portCount = ports != null ? ports.Count : 0;
            _firePoints = new Transform[portCount];
            for (int index = 0; index < portCount; index++)
            {
                _firePoints[index] = ports[index]?.FirePoint;
            }

            _initialized = _centralCore != null;
        }

        /// <summary>缓存所属中央核心，后续由初始化流程补齐其余依赖。</summary>
        private void Awake()
        {
            _centralCore = GetComponent<CentralCore>();
        }

        /// <summary>在启动时尝试从所属中央核心完成绑定。</summary>
        private void Start()
        {
            TryInitializeFromCore();
            if (_input != null)
            {
                _mouseFireSubscription = _input.MouseFireHeld.Subscribe(HandleMouseFireChanged);
            }
        }

        private void OnDestroy()
        {
            _mouseFireSubscription?.Dispose();
        }

        private void HandleMouseFireChanged(bool isHeld)
        {
            _mouseFireHeld = isHeld;
        }

        /// <summary>按唯一攻击模式采样输入、更新鼠标瞄准并提交攻击请求。</summary>
        private void Update()
        {
            if (!_initialized)
            {
                TryInitializeFromCore();
            }

            if (!_initialized || _firePoints == null || _centralCore == null)
            {
                return;
            }

            if (_input != null && _input.CurrentAttackInputState == AttackInputState.Blocked)
            {
                return;
            }

            TransmitterAttackMode attackMode = _attackModeProvider != null
                ? _attackModeProvider.AttackMode
                : TransmitterAttackMode.Automatic;
            Vector3 mouseTargetPosition = Vector3.zero;
            IDamageable mouseTarget = null;
            bool mouseHeld = attackMode == TransmitterAttackMode.Mouse
                && _input != null
                && _mouseFireHeld
                && _camera != null;
            Ray mouseRay = mouseHeld
                ? _camera.ScreenPointToRay(_input.PointerPosition)
                : default;
            bool hasMouseTarget = mouseHeld
                && TryGetMouseTarget(mouseRay, out mouseTargetPosition, out mouseTarget);
            IDamageable markedTarget = attackMode == TransmitterAttackMode.MarkedTarget
                ? _markedTarget?.Target
                : null;

            int maxPorts = Mathf.Min(_firePoints.Length, _centralCore.PortCount);
            if (_input != null)
            {
                maxPorts = Mathf.Min(maxPorts, _input.MaxPorts);
            }

            for (int portIndex = 0; portIndex < maxPorts; portIndex++)
            {
                CentralTransmitterRuntime port = _centralCore.GetPort(portIndex);
                if (port == null || port.IsLocked || _firePoints[portIndex] == null || port.So == null)
                {
                    continue;
                }

                IDamageable portTarget = attackMode switch
                {
                    TransmitterAttackMode.Mouse => null,
                    TransmitterAttackMode.MarkedTarget => markedTarget,
                    _ => GetAutomaticTarget(portIndex)
                };
                bool shouldFire = attackMode switch
                {
                    TransmitterAttackMode.Automatic => portTarget.IsAliveAndValid(),
                    TransmitterAttackMode.Key => _input != null && _input.IsPortFireHeld(portIndex),
                    TransmitterAttackMode.Mouse => mouseHeld,
                    TransmitterAttackMode.MarkedTarget => portTarget.IsAliveAndValid(),
                    _ => false
                };
                if (!shouldFire)
                {
                    continue;
                }

                ProjectileProfileSO profile = port.So.defaultProjectileProfile;
                if (profile == null)
                {
                    continue;
                }

                Transform firePoint = _firePoints[portIndex];
                if (attackMode == TransmitterAttackMode.Mouse)
                {
                    if (hasMouseTarget)
                    {
                        AimAtPosition(firePoint, mouseTargetPosition);
                    }
                    else
                    {
                        AimAlongMouseRay(firePoint, mouseRay.direction);
                    }
                }
                else if (attackMode == TransmitterAttackMode.MarkedTarget)
                {
                    AimAtPosition(firePoint, portTarget.Position);
                }

                var context = new TransmitterAttackInput(
                    _centralCore.GetInstanceID(),
                    portIndex,
                    profile,
                    firePoint.position,
                    firePoint.forward,
                    port.So.baseDamage,
                    port.So.baseRange,
                    port.So.attacksPerSecond,
                    port.So.projectileCount,
                    port.So.penetration,
                    port.So.criticalChance,
                    port.So.criticalDamage,
                    port.PortId,
                    port.FireSound,
                    port.FireEffectId,
                    firePoint,
                    attackMode == TransmitterAttackMode.Mouse ? mouseTarget : portTarget,
                    port.So.projectileSpawnOffset);

                if (_attackService != null && _attackService.TryExecuteAttack(in context))
                {
                    TriggerPuzzleMove(portIndex);
                }
            }
        }

        /// <summary>Queues the Inspector-mapped move once for a successfully submitted port attack.</summary>
        /// <param name="portIndex">Index of the port in the central configuration.</param>
        private void TriggerPuzzleMove(int portIndex)
        {
            if (_diamondTwist == null ||
                _puzzleMoves == null ||
                portIndex < 0 ||
                portIndex >= MaxPuzzlePorts ||
                portIndex >= _puzzleMoves.Length)
            {
                return;
            }

            _diamondTwist.RequestTurn(_puzzleMoves[portIndex]);
        }

        /// <summary>读取指定端口的自动目标，并优先返回全局聚焦目标。</summary>
        /// <param name="portIndex">发射器端口索引。</param>
        /// <returns>当前有效目标；没有目标时返回 null。</returns>
        private IDamageable GetAutomaticTarget(int portIndex)
        {
            IDamageable target = _detector != null ? _detector.GetTarget(portIndex) : null;
            IDamageable focusedTarget = _globalEffects?.GetFocusedTarget();
            if (focusedTarget.IsAliveAndValid())
            {
                return focusedTarget;
            }

            return target.IsAliveAndValid() ? target : null;
        }

        /// <summary>从鼠标射线解析有效敌人和命中点。</summary>
        /// <param name="ray">相机鼠标射线。</param>
        /// <param name="targetPosition">射线命中的世界坐标。</param>
        /// <param name="target">射线命中的可受伤害目标。</param>
        /// <returns>命中有效敌人时返回 true。</returns>
        private bool TryGetMouseTarget(
            Ray ray,
            out Vector3 targetPosition,
            out IDamageable target)
        {
            targetPosition = Vector3.zero;
            target = null;

            int targetMask = _detector != null
                ? _detector.enemyLayerMask.value
                : LayerMask.GetMask("Enemy");
            if (targetMask == 0)
            {
                return false;
            }

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    _camera.farClipPlane,
                    targetMask,
                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            target = hit.collider.GetComponentInParent<IDamageable>();
            if (!target.IsAliveAndValid())
            {
                return false;
            }

            targetPosition = hit.point;
            return true;
        }

        /// <summary>将发射点朝向给定的世界坐标。</summary>
        /// <param name="firePoint">需要转向的发射点。</param>
        /// <param name="targetPosition">目标世界坐标。</param>
        private static void AimAtPosition(Transform firePoint, Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - firePoint.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            firePoint.rotation = Quaternion.LookRotation(direction.normalized);
        }

        /// <summary>将发射点沿相机鼠标射线方向瞄准。</summary>
        /// <param name="firePoint">需要转向的发射点。</param>
        /// <param name="rayDirection">相机鼠标射线的世界方向。</param>
        private static void AimAlongMouseRay(Transform firePoint, Vector3 rayDirection)
        {
            if (rayDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            firePoint.rotation = Quaternion.LookRotation(rayDirection.normalized);
        }

        /// <summary>从所属中央核心读取依赖并执行一次初始化。</summary>
        private void TryInitializeFromCore()
        {
            if (_centralCore == null)
            {
                _centralCore = GetComponent<CentralCore>();
            }

            if (_centralCore == null || _centralCore.Ports == null)
            {
                return;
            }

            if (_detector == null)
            {
                _detector = GetComponent<PhysicsCentralDetector>();
            }

            Initialize(_centralCore, _centralCore.Ports, _detector);
        }
    }
}
