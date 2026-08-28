using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Play
{
    /// <summary>
    /// 单个端口的运行时对象，封装端口配置、属性、模型、FirePoint 和锁定状态。
    /// 不依赖中心炮台或任何表现控制器。
    /// </summary>
    public sealed class CentralTransmitterRuntime : IDisposable
    {
        private readonly IObjectResolver _resolver;
        private GameObject _modelInstance;
        private bool _disposed;

        public TransmitterSO So { get; }
        public int PortIndex { get; }
        public int TransmitterIndex => PortIndex;
        public string TransmitterId { get; }
        public string PortId { get; }
        public Transform FirePoint { get; private set; }
        public TransmitterAttributes Attributes { get; }
        public bool IsLocked { get; private set; }

        public CentralTransmitterRuntime(
            TransmitterSO so,
            int index,
            Transform parent,
            IObjectResolver resolver,
            TransmitterAttributes templateAttributes = null)
        {
            So = so ?? throw new ArgumentNullException(nameof(so));
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            PortIndex = index;
            TransmitterId = string.IsNullOrWhiteSpace(so.portName) ? $"Transmitter_{index}" : so.portName;
            PortId = $"{parent.name}_Transmitter{index}_{TransmitterId}";
            _resolver = resolver;
            IsLocked = so.isInitiallyLocked;

            GameObject firePointObject = new GameObject(PortId);
            firePointObject.transform.SetParent(parent, false);
            FirePoint = firePointObject.transform;

            Attributes = templateAttributes != null
                ? new TransmitterAttributes(templateAttributes)
                : new TransmitterAttributes();

            InstantiateModel(FirePoint);
        }

        /// <summary>解锁端口并切换到正常模型。</summary>
        public void Unlock()
        {
            if (_disposed || !IsLocked)
                return;

            IsLocked = false;
            ReplaceLockedModelWithNormalModel();
        }

        /// <summary>
        /// 将端口 FirePoint 对齐到球冠底面圆周。
        /// 球心由上层编排对象提供，端口自身不缓存球心。
        /// </summary>
        public void AlignToSphere(Vector3 sphereCenter, float capHeight)
        {
            if (_disposed || FirePoint == null)
                return;

            Vector3 turretPosition = FirePoint.parent != null
                ? FirePoint.parent.position
                : FirePoint.position;

            CapLayout.CalculatePortPose(
                sphereCenter,
                turretPosition,
                capHeight,
                So.portName,
                out Vector3 position);

            FirePoint.position = position;
        }

        private void ReplaceLockedModelWithNormalModel()
        {
            if (_modelInstance != null)
            {
                DestroyUnityObject(_modelInstance);
                _modelInstance = null;
            }

            InstantiateModel(FirePoint);
        }

        private void InstantiateModel(Transform parent)
        {
            GameObject prefab = IsLocked && So.lockedModelPrefab != null
                ? So.lockedModelPrefab
                : So.portModelPrefab;

            if (prefab == null || parent == null)
                return;

            _modelInstance = _resolver != null
                ? _resolver.Instantiate(prefab, parent)
                : UnityEngine.Object.Instantiate(prefab, parent);

            _modelInstance.transform.localPosition = Vector3.zero;
            _modelInstance.transform.localRotation = Quaternion.identity;
            _modelInstance.SetActive(!IsLocked);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_modelInstance != null)
            {
                DestroyUnityObject(_modelInstance);
                _modelInstance = null;
            }

            if (FirePoint != null)
            {
                DestroyUnityObject(FirePoint.gameObject);
                FirePoint = null;
            }
        }

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
