using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Play
{
    /// <summary>
    /// 枢纽 - 胶水类
    /// 端口集合、球面定位数据和表现控制器。
    /// </summary>
    public class CentralCore : MonoBehaviour
    {
        [Header("自身数据 移动和位置相关数据")] [SerializeField]
        private CentralSO centralSo;

        public CentralSO CentralSoConfig => centralSo;

        private CentralAttributes centralAttributes;

        [Header("自身数据")] [SerializeField] private SphereWalker _sphereWalker;
        public SphereWalker SphereWalker => _sphereWalker;

        [SerializeField] private TransmitterAttributes baseTransmitterAttributes;

        [Header("球冠更新")] [Tooltip("端口位置更新间隔帧数（降低开销）。位置发生变化时会立即更新。")] [Min(1)] [SerializeField]
        private int alignIntervalFrames = 5;

        private CentralTransmitterManager _portManager;
        private GameObject _turretModel;
        private Vector3 _lastAlignPosition;
        private int _alignFrameCounter;
        private bool _initialized;

        [Header("调试")] 
        [SerializeField] private bool showGizmos = true;

        [Inject] private IObjectResolver _resolver;

        public float Damage
        {
            get
            {
                if (_portManager == null || _portManager.PortCount == 0)
                    return 0f;

                float sum = 0f;
                IReadOnlyList<CentralTransmitterRuntime> ports = _portManager.Ports;
                for (int i = 0; i < ports.Count; i++)
                    sum += ports[i].Attributes.Damage;

                return sum / ports.Count;
            }
        }

        public float Range
        {
            get
            {
                if (_portManager == null || _portManager.PortCount == 0)
                    return 0f;

                float maxRange = 0f;
                IReadOnlyList<CentralTransmitterRuntime> ports = _portManager.Ports;
                for (int i = 0; i < ports.Count; i++)
                {
                    if (ports[i].Attributes.Range > maxRange)
                        maxRange = ports[i].Attributes.Range;
                }

                return maxRange;
            }
        }

        public float FireRate
        {
            get
            {
                if (_portManager == null || _portManager.PortCount == 0)
                    return 0f;

                float sum = 0f;
                IReadOnlyList<CentralTransmitterRuntime> ports = _portManager.Ports;
                for (int i = 0; i < ports.Count; i++)
                    sum += ports[i].Attributes.FireRate;

                return sum / ports.Count;
            }
        }

        public int PortCount => _portManager?.PortCount ?? 0;
        public int ActivePortCount => _portManager?.ActivePortCount ?? 0;
        public int LockedPortCount => _portManager?.LockedPortCount ?? 0;
        public int MaxPorts => PortCount;
        public CentralTransmitterManager PortManager => _portManager;
        public IReadOnlyList<CentralTransmitterRuntime> Ports => _portManager?.Ports;

        public CentralTransmitterRuntime TryExpandPort()
        {
            return _portManager?.UnlockNextPort();
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            _portManager?.Dispose();
            _portManager = null;

            if (_turretModel != null)
            {
                DestroyUnityObject(_turretModel);
                _turretModel = null;
            }
        }

        private void Update()
        {
            UpdateMultiPort();
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            if (centralSo == null || centralSo.Transmitters == null || centralSo.Transmitters.Length == 0)
            {
                Debug.LogWarning($"[CentralCore] {name}: 未配置中心炮台或发射口。");
                return;
            }

            if (_sphereWalker == null)
                _sphereWalker = GetComponentInParent<SphereWalker>();

            _portManager = new CentralTransmitterManager(
                centralSo,
                transform,
                _resolver,
                baseTransmitterAttributes);
            centralAttributes = new CentralAttributes(
                centralSo.detectionRadius,
                centralSo.baseRotationSpeed);

            CreateCentralModel();
            BindRuntimeComponents();
            _initialized = true;
        }

        private void CreateCentralModel()
        {
            if (centralSo.modelPrefab == null)
                return;

            _turretModel = Instantiate(centralSo.modelPrefab, transform);
            _turretModel.transform.localPosition = Vector3.zero;
            _turretModel.transform.localRotation = Quaternion.identity;
        }

        private void BindRuntimeComponents()
        {
            PhysicsCentralDetector detector = GetComponent<PhysicsCentralDetector>();
            if (detector != null)
                detector.Initialize(_portManager.PortCount, centralAttributes);

            IReadOnlyList<CentralTransmitterRuntime> ports = _portManager.Ports;

            TransmitterAimController aimController = GetComponent<TransmitterAimController>();
            if (aimController != null)
                aimController.Initialize(ports, detector, _sphereWalker);

            TransmitterFireController fireController = GetComponent<TransmitterFireController>();
            if (fireController != null)
                fireController.Initialize(this, ports, detector);

            BindTrajectoryDisplays(detector, fireController, ports);
        }

        private void BindTrajectoryDisplays(
            PhysicsCentralDetector detector,
            TransmitterFireController fireController,
            IReadOnlyList<CentralTransmitterRuntime> ports)
        {
            TransmitterTrajectoryDisplay[] displays =
                GetComponentsInChildren<TransmitterTrajectoryDisplay>(true);

            for (int displayIndex = 0; displayIndex < displays.Length; displayIndex++)
            {
                TransmitterTrajectoryDisplay display = displays[displayIndex];
                for (int portIndex = 0; portIndex < ports.Count; portIndex++)
                {
                    CentralTransmitterRuntime port = ports[portIndex];
                    if (port == null || port.FirePoint == null)
                        continue;

                    if (display.transform == port.FirePoint || display.transform.IsChildOf(port.FirePoint))
                    {
                        display.Initialize(this, detector, fireController, port);
                        break;
                    }
                }
            }
        }

        private void UpdateMultiPort()
        {
            if (_portManager == null || _sphereWalker == null)
                return;

            Vector3 sphereCenter = _sphereWalker.GetEffectiveCenter();
            bool positionChanged = (_lastAlignPosition - transform.position).sqrMagnitude > 1e-6f;
            _alignFrameCounter++;

            int interval = Mathf.Max(1, alignIntervalFrames);
            if (positionChanged || _alignFrameCounter >= interval)
            {
                IReadOnlyList<CentralTransmitterRuntime> ports = _portManager.Ports;
                float capHeight = centralSo != null ? centralSo.capHeight : 1f;
                for (int i = 0; i < ports.Count; i++)
                    ports[i].AlignToSphere(sphereCenter, capHeight);

                _lastAlignPosition = transform.position;
                _alignFrameCounter = 0;
            }

            if (_turretModel != null)
            {
                Vector3 directionToCenter = sphereCenter - transform.position;
                if (directionToCenter.sqrMagnitude > 0.0001f)
                    _turretModel.transform.rotation = Quaternion.LookRotation(directionToCenter.normalized);
            }
        }

        #region 公共 API

        public CentralTransmitterRuntime GetPort(int index)
        {
            return _portManager?.GetPort(index);
        }

        #endregion

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }
    }
}