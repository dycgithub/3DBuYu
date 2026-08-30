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

        [Header("发射器背包绑定")]
        [Tooltip("按 CentralSO.Transmitters 顺序填写发射器背包；不使用物体名称推断映射。")]
        [SerializeField] private GridView[] transmitterBackpacks;

        [SerializeField] private TransmitterAttributes baseTransmitterAttributes;

        [Header("球冠更新")] [Tooltip("端口位置更新间隔帧数（降低开销）。位置发生变化时会立即更新。")] [Min(1)] [SerializeField]
        private int alignIntervalFrames = 5;

        private CentralTransmitterManager _portManager;
        private GameObject _turretModel;
        private Vector3 _lastAlignPosition;
        private int _alignFrameCounter;
        private bool _initialized;

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
            BindTransmitterBackpacks();
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

        private void BindTransmitterBackpacks()
        {
            int portCount = _portManager != null ? _portManager.PortCount : 0;
            if (transmitterBackpacks == null || transmitterBackpacks.Length != portCount)
            {
                int configuredCount = transmitterBackpacks != null ? transmitterBackpacks.Length : 0;
                Debug.LogWarning(
                    $"[CentralCore] {name}: 发射器背包数量 {configuredCount} 与端口数量 {portCount} 不匹配。",
                    this);
            }

            var assignedBackpacks = new HashSet<GridView>();
            int bindCount = Mathf.Min(
                transmitterBackpacks != null ? transmitterBackpacks.Length : 0,
                portCount);

            for (int index = 0; index < bindCount; index++)
            {
                GridView backpack = transmitterBackpacks[index];
                if (backpack == null)
                {
                    Debug.LogWarning($"[CentralCore] {name}: 发射器索引 {index} 未配置背包。", this);
                    continue;
                }

                if (backpack.GridType != GridType.TransmitterBackpack)
                {
                    Debug.LogWarning(
                        $"[CentralCore] {name}: 发射器索引 {index} 引用了非 TransmitterBackpack 网格 {backpack.name}。",
                        backpack);
                    continue;
                }

                if (!assignedBackpacks.Add(backpack))
                {
                    Debug.LogWarning(
                        $"[CentralCore] {name}: 背包 {backpack.name} 被重复分配，跳过索引 {index}。",
                        backpack);
                    continue;
                }

                backpack.AssignTransmitter(index);
            }
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