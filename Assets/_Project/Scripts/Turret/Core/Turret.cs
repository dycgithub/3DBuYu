using UnityEngine;
using System.Collections.Generic;
using InventorySystem;
using VContainer;

namespace TurretSystem
{
    public class Turret : MonoBehaviour
    {
        [Header("炮塔配置")]
        [SerializeField] private TurretBase turretBase;

        [Header("球面设置")]
        [SerializeField] private SphereWalker _sphereWalker;

        [Header("端口背包设置")]
        [SerializeField] private PortInventorySettings portInventorySettings;

        [SerializeField] private PortAttributes basePortAttributes;

        [Header("球冠更新")]
        [Tooltip("端口位置更新间隔帧数（降低开销）。位置发生变化时会立即更新。")]
        [Min(1)]
        [SerializeField] private int alignIntervalFrames = 5;

        [Header("调试")]
        [SerializeField] private bool showGizmos = true;

        public TurretBase TurretBaseConfig => turretBase;
        public SphereWalker SphereWalker => _sphereWalker;
        public PortInventorySettings PortInventorySettings => portInventorySettings;
        public bool ShowGizmos => showGizmos;

        private TurretPortManager _portManager;
        private GameObject _turretModel;
        private TurretInventory _turretInventory;
        private StorageInventory _storageInventory;

        private Vector3 _lastAlignPosition;
        private int _alignFrameCounter;

        [Inject] private DurabilityManager _durability;
        [Inject] private IObjectResolver _resolver;
        [Inject] private PlayerLoadout _loadout;
        [Inject] private GameSystem.PlayerStorage _storage;

        public float Damage
        {
            get
            {
                if (_portManager == null || _portManager.PortCount == 0) return 0f;
                float sum = 0f;
                foreach (var port in _portManager.Ports)
                    sum += port.Attributes.Damage;
                return sum / _portManager.PortCount;
            }
        }

        public float Range
        {
            get
            {
                if (_portManager == null || _portManager.PortCount == 0) return 0f;
                float maxRange = 0f;
                foreach (var port in _portManager.Ports)
                {
                    if (port.Attributes.Range > maxRange)
                        maxRange = port.Attributes.Range;
                }
                return maxRange;
            }
        }

        public float FireRate
        {
            get
            {
                if (_portManager == null || _portManager.PortCount == 0) return 0f;
                float sum = 0f;
                foreach (var port in _portManager.Ports)
                    sum += port.Attributes.FireRate;
                return sum / _portManager.PortCount;
            }
        }

        public int PortCount => _portManager?.PortCount ?? 0;
        public int ActivePortCount => _portManager?.ActivePortCount ?? 0;
        public int LockedPortCount => _portManager?.LockedPortCount ?? 0;
        public int MaxPorts => _portManager?.PortCount ?? 0;
        public TurretPortManager PortManager => _portManager;
        public TurretInventory TurretInventory => _turretInventory;
        public StorageInventory StorageInventory => _storageInventory;

        public TurretPortRuntime TryExpandPort()
            => _portManager?.UnlockNextPort();

        #region Unity 生命周期

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            _portManager?.Dispose();
        }

        private void Update()
        {
            UpdateMultiPort();
        }

        #endregion

        #region 初始化

        private void Initialize()
        {
            if (turretBase == null || turretBase.firingPorts == null || turretBase.firingPorts.Length == 0)
            {
                Debug.LogWarning($"[Turret] {name}: 未配置 turretBase 或 firingPorts。");
                return;
            }

            if (_sphereWalker == null) _sphereWalker = GetComponentInParent<SphereWalker>();

            InitializeMultiPort();
        }

        private void InitializeMultiPort()
        {
            if (portInventorySettings == null)
                portInventorySettings = PortInventorySettings.CreateDefault();

            _portManager = new TurretPortManager(
                turretBase,
                transform,
                _resolver,
                portInventorySettings,
                basePortAttributes,
                _loadout);

            _turretInventory = _loadout != null ? _loadout.TurretInventory : new TurretInventory(
                this,
                turretBase.turretInventoryColumns,
                turretBase.turretInventoryRows,
                turretBase.detectionRadius,
                turretBase.baseRotationSpeed);

            _storageInventory = _storage != null ? _storage.Inventory : new StorageInventory(this);

            if (_durability != null)
            {
                _durability.RegisterInventory(_turretInventory);
                foreach (var port in _portManager.Ports)
                    _durability.RegisterInventory(port.Inventory);
            }

            if (turretBase.modelPrefab != null)
            {
                _turretModel = Instantiate(turretBase.modelPrefab, transform);
                _turretModel.transform.localPosition = Vector3.zero;
                _turretModel.transform.localRotation = Quaternion.identity;
            }

            if (_portManager == null || _portManager.PortCount == 0) return;

            var firePoints = new Transform[_portManager.PortCount];
            for (int i = 0; i < _portManager.PortCount; i++)
                firePoints[i] = _portManager.GetPort(i).FirePoint;

            var detector = GetComponent<PhysicsTurretDetector>();
            if (detector != null)
            {
                detector.SetPortCount(_portManager.PortCount);
                detector.enemyLayerMask = 1 << LayerMask.NameToLayer("Enemy");
                detector.BindAttributes(_turretInventory.Attributes);
            }

            var controller = GetComponent<PortFireController>();
            if (controller != null)
            {
                controller.firePoints = firePoints;
                controller.detector = detector;
            }
        }

        #endregion

        #region 运行时更新

        private void UpdateMultiPort()
        {
            if (_portManager == null) return;

            if (_sphereWalker == null) return;

            Vector3 sphereCenter = _sphereWalker.GetEffectiveCenter();

            // 检查 turret 位置是否变化，或达到间隔帧数
            bool positionChanged =
                (_lastAlignPosition - transform.position).sqrMagnitude > 1e-6f;

            _alignFrameCounter++;

            if (positionChanged || _alignFrameCounter >= alignIntervalFrames)
            {
                _portManager.SetSphereCenter(sphereCenter);
                _lastAlignPosition = transform.position;
                _alignFrameCounter = 0;
            }

            // Turret 模型始终朝向球心
            if (_turretModel != null)
            {
                Vector3 dirToCenter = (sphereCenter - transform.position).normalized;
                _turretModel.transform.rotation = Quaternion.LookRotation(dirToCenter);
            }
        }

        #endregion

        #region 公共 API

        public TurretPortRuntime GetPort(int index)
        {
            return _portManager?.GetPort(index);
        }

        public IReadOnlyList<PortInventory> GetAllPortInventories()
        {
            return _portManager?.GetAllPortInventories();
        }

        #endregion
    }
}
