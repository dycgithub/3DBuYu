using System;
using System.Collections.Generic;
using System.Linq;
using Interfaces;
using UnityEngine;
using VContainer;

namespace TurretSystem
{
    public class TurretPortManager : IDisposable
    {
        public IReadOnlyList<TurretPortRuntime> Ports => _ports;

        public int PortCount => _ports.Count;

        public TurretBase TurretBaseConfig { get; }

        public event Action<int, int> OnPortCountChanged;

        private readonly List<TurretPortRuntime> _ports = new List<TurretPortRuntime>();
        private readonly PortInventorySettings _invSettings;
        private readonly Transform _parent;
        private readonly IObjectResolver _resolver;
        private Vector3 _sphereCenter;

        public TurretPortManager(
            TurretBase turretBase,
            Transform parent,
            IObjectResolver resolver,
            PortInventorySettings invSettings = null,
            PortAttributes templateAttributes = null,
            PlayerLoadout loadout = null)
        {
            TurretBaseConfig = turretBase ?? throw new ArgumentNullException(nameof(turretBase));
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _invSettings = invSettings ?? PortInventorySettings.CreateDefault();
            _resolver = resolver;

            CreatePorts(templateAttributes, loadout);

            OnPortCountChanged?.Invoke(ActivePortCount, _ports.Count);
        }

        public void SetSphereCenter(Vector3 sphereCenter)
        {
            _sphereCenter = sphereCenter;

            float capHeight = TurretBaseConfig?.capHeight ?? 1f;
            for (int i = 0; i < _ports.Count; i++)
            {
                _ports[i].AlignToSphere(sphereCenter, capHeight);
            }
        }

        private void CreatePorts(PortAttributes templateAttributes, PlayerLoadout loadout)
        {
            var configs = TurretBaseConfig.firingPorts;
            if (configs == null || configs.Length == 0)
            {
                Debug.LogWarning($"[TurretPortManager] TurretBase '{TurretBaseConfig.displayName}' 未配置发射口。");
                return;
            }

            for (int i = 0; i < configs.Length; i++)
            {
                PortInventory boundInventory = null;
                if (loadout != null && loadout.PortInventories != null && i < loadout.PortInventories.Count)
                    boundInventory = loadout.PortInventories[i];

                var port = new TurretPortRuntime(
                    configs[i],
                    i,
                    _parent,
                    _invSettings,
                    _resolver,
                    templateAttributes,
                    boundInventory);

                _ports.Add(port);
            }
        }

        public int LockedPortCount => _ports.Count(p => p.IsLocked);

        public int ActivePortCount => _ports.Count(p => !p.IsLocked);

        public TurretPortRuntime UnlockNextPort()
        {
            foreach (var port in _ports)
            {
                if (port.IsLocked)
                {
                    port.Unlock();
                    Debug.Log($"[TurretPortManager] 端口已解锁: {port.PortId}");
                    int active = ActivePortCount;
                    OnPortCountChanged?.Invoke(active, _ports.Count);
                    return port;
                }
            }
            return null;
        }

        public TurretPortRuntime GetPort(int index)
        {
            if (index < 0 || index >= _ports.Count) return null;
            return _ports[index];
        }

        public TurretPortRuntime GetPortById(string portId)
        {
            return _ports.Find(p => p.PortId == portId);
        }

        public IReadOnlyList<PortInventory> GetAllPortInventories()
        {
            var inventories = new List<PortInventory>();
            foreach (var port in _ports)
            {
                inventories.Add(port.Inventory);
            }
            return inventories;
        }

        public void Dispose()
        {
            foreach (var port in _ports)
            {
                port.Dispose();
            }
            _ports.Clear();
        }
    }
}
