using System.Collections.Generic;
using Interfaces;
using Services;
using CombatSystem;
using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>
    /// 把发射器输入和自动目标转交给集中式射击服务。
    /// 端口 FirePoint 和探测器只在初始化时绑定，避免控制器对炮台内部结构进行写入。
    /// </summary>
    public class TransmitterFireController : MonoBehaviour
    {
        [Header("子弹")]
        public BulletProfile bulletProfile;
        public float fireRate = 2f;

        [Header("开火模式")]
        public bool autoFire = true;

        private CentralCore _centralCore;
        private PhysicsCentralDetector _detector;
        private Transform[] _firePoints;
        private bool _initialized;

        [Inject] private ITransmitterAttackService _attackService;
        [Inject] private IInputService _input;

        public BulletProfile BulletProfile => bulletProfile;

        public void Initialize(
            CentralCore core,
            IReadOnlyList<CentralTransmitterRuntime> ports,
            PhysicsCentralDetector targetDetector)
        {
            _centralCore = core;
            _detector = targetDetector;

            int portCount = ports != null ? ports.Count : 0;
            _firePoints = new Transform[portCount];
            for (int i = 0; i < portCount; i++)
                _firePoints[i] = ports[i]?.FirePoint;

            _initialized = _centralCore != null;
        }

        private void Awake()
        {
            _centralCore = GetComponent<CentralCore>();
        }

        private void Start()
        {
            TryInitializeFromCore();
        }

        private void Update()
        {
            if (!_initialized)
                TryInitializeFromCore();

            if (!_initialized)
                return;

            if (_attackService == null || _centralCore == null || _firePoints == null)
                return;

            int maxPorts = Mathf.Min(_firePoints.Length, _centralCore.PortCount);
            if (_input != null)
                maxPorts = Mathf.Min(maxPorts, _input.MaxPorts);

            for (int portIndex = 0; portIndex < maxPorts; portIndex++)
            {
                CentralTransmitterRuntime port = _centralCore.GetPort(portIndex);
                if (port == null || port.IsLocked || _firePoints[portIndex] == null)
                    continue;

                bool manualHeld = _input != null && _input.IsPortFireHeld(portIndex);
                IDamageable target = _detector != null
                    ? _detector.GetTarget(portIndex)
                    : null;
                bool autoRequested = autoFire && target.IsAliveAndValid();

                if (!manualHeld && !autoRequested)
                    continue;

                float rate = fireRate;
                float damageMultiplier = 1f;
                float rangeMultiplier = 1f;
                int projectileCount = 1;
                int penetration = 0;
                float criticalChance = 0f;
                float criticalDamage = 1f;

                if (port.Attributes != null)
                {
                    if (port.Attributes.BaseDamage > 0f)
                        damageMultiplier = Mathf.Max(
                            0f,
                            port.Attributes.Damage / port.Attributes.BaseDamage);

                    if (port.Attributes.BaseRange > 0f)
                        rangeMultiplier = Mathf.Max(
                            0f,
                            port.Attributes.Range / port.Attributes.BaseRange);

                    projectileCount = Mathf.Max(1, port.Attributes.ProjectileCount);
                    penetration = Mathf.Max(0, port.Attributes.Penetration);
                    criticalChance = port.Attributes.CriticalChance;
                    criticalDamage = port.Attributes.CriticalDamage;

                    if (port.Attributes.FireRate > 0f)
                        rate *= port.Attributes.FireRate;
                }

                BulletProfile profile = port.So != null && port.So.defaultBullet != null
                    ? port.So.defaultBullet
                    : bulletProfile;

                var context = new TransmitterAttackInput(
                    _centralCore.GetInstanceID(),
                    portIndex,
                    profile,
                    _firePoints[portIndex].position,
                    _firePoints[portIndex].forward,
                    rate,
                    damageMultiplier,
                    rangeMultiplier,
                    projectileCount,
                    penetration,
                    criticalChance,
                    criticalDamage);

                _attackService.TryExecute(in context);
            }
        }

        private void TryInitializeFromCore()
        {
            if (_centralCore == null)
                _centralCore = GetComponent<CentralCore>();

            if (_centralCore == null || _centralCore.Ports == null)
                return;

            if (_detector == null)
                _detector = GetComponent<PhysicsCentralDetector>();

            Initialize(_centralCore, _centralCore.Ports, _detector);
        }
    }
}
