using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using Services;
using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>
    /// 把发射器输入和自动目标转交给集中式攻击服务。
    /// <para>端口的基础攻击数值和子弹配置全部来自对应的 <see cref="TransmitterSO"/>。</para>
    /// <para>本组件只负责输入采样、目标读取和攻击提交，不保存攻击配置。</para>
    /// </summary>
    public class TransmitterFireController : MonoBehaviour
    {
        /// <summary>是否在存在自动目标时持续提交攻击。</summary>
        [Header("开火模式")]
        public bool autoFire = true;

        private CentralCore _centralCore;
        private PhysicsCentralDetector _detector;
        private Transform[] _firePoints;
        private bool _initialized;

        [Inject] private ITransmitterAttackService _attackService;
        [Inject] private IInputService _input;

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
        }

        /// <summary>读取各端口输入，生成攻击输入并提交给攻击服务。</summary>
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

                bool manualHeld = _input != null && _input.IsPortFireHeld(portIndex);
                IDamageable target = _detector != null
                    ? _detector.GetTarget(portIndex)
                    : null;
                bool autoRequested = autoFire && target.IsAliveAndValid();
                if (!manualHeld && !autoRequested)
                {
                    continue;
                }

                ProjectileProfileSO profile = port.So.defaultProjectileProfile;
                if (profile == null)
                {
                    continue;
                }

                Transform firePoint = _firePoints[portIndex];
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
                    port.PortId);

                _attackService?.TryExecuteAttack(in context);
            }
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
