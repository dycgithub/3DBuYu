using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>
    /// 管理中心炮台的端口集合。
    /// 该类只负责创建、查询、解锁和释放端口运行时对象，不参与球面定位或控制器行为。
    /// </summary>
    public sealed class CentralTransmitterManager : IDisposable
    {
        private readonly List<CentralTransmitterRuntime> _ports = new List<CentralTransmitterRuntime>();
        private readonly Transform _parent;
        private readonly IObjectResolver _resolver;
        private int _lockedPortCount;
        private bool _disposed;

        public IReadOnlyList<CentralTransmitterRuntime> Ports => _ports;
        public int PortCount => _ports.Count;
        public int LockedPortCount => _lockedPortCount;
        public int ActivePortCount => _ports.Count - _lockedPortCount;
        public CentralSO CentralSoConfig { get; }

        /// <summary>端口状态发生变化时通知观察者，参数为活跃数量和总数量。</summary>
        public event Action<int, int> OnPortCountChanged;

        public CentralTransmitterManager(
            CentralSO centralSo,
            Transform parent,
            IObjectResolver resolver,
            TransmitterAttributes templateAttributes = null)
        {
            CentralSoConfig = centralSo ?? throw new ArgumentNullException(nameof(centralSo));
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _resolver = resolver;

            CreatePorts(templateAttributes);
            OnPortCountChanged?.Invoke(ActivePortCount, PortCount);
        }

        private void CreatePorts(TransmitterAttributes templateAttributes)
        {
            TransmitterSO[] configs = CentralSoConfig.Transmitters;
            if (configs == null || configs.Length == 0)
            {
                Debug.LogWarning($"[CentralTransmitterManager] '{CentralSoConfig.displayName}' 未配置发射口。");
                return;
            }

            for (int i = 0; i < configs.Length; i++)
            {
                CentralTransmitterRuntime port = new CentralTransmitterRuntime(
                    configs[i],
                    i,
                    _parent,
                    _resolver,
                    templateAttributes);

                _ports.Add(port);
                if (port.IsLocked)
                    _lockedPortCount++;
            }
        }

        public CentralTransmitterRuntime UnlockNextPort()
        {
            if (_disposed)
                return null;

            for (int i = 0; i < _ports.Count; i++)
            {
                CentralTransmitterRuntime port = _ports[i];
                if (!port.IsLocked)
                    continue;

                port.Unlock();
                _lockedPortCount--;
                Debug.Log($"[CentralTransmitterManager] 端口已解锁: {port.PortId}");
                OnPortCountChanged?.Invoke(ActivePortCount, PortCount);
                return port;
            }

            return null;
        }

        public CentralTransmitterRuntime GetPort(int index)
        {
            if (index < 0 || index >= _ports.Count)
                return null;

            return _ports[index];
        }

        public CentralTransmitterRuntime GetPortById(string portId)
        {
            if (string.IsNullOrEmpty(portId))
                return null;

            for (int i = 0; i < _ports.Count; i++)
            {
                if (_ports[i].PortId == portId)
                    return _ports[i];
            }

            return null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            for (int i = 0; i < _ports.Count; i++)
                _ports[i].Dispose();

            _ports.Clear();
            _lockedPortCount = 0;
        }
    }
}
