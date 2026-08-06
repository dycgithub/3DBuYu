using System;
using Interfaces;
using UnityEngine;
using VContainer;

namespace TurretSystem
{
    public class TurretPortRuntime : IDisposable
    {
        public TurretPortConfig Config { get; }

        private readonly IObjectResolver _resolver;

        public int PortIndex { get; }

        public string PortId { get; }

        public Transform FirePoint { get; private set; }

        public PortInventory Inventory { get; }

        public PortAttributes Attributes => Inventory?.Attributes;

        private GameObject _modelInstance;

        public bool IsLocked { get; set; }

        public void Unlock()
        {
            if (!IsLocked) return;
            IsLocked = false;
            if (_modelInstance != null && Config.lockedModelPrefab != null)
            {
                UnityEngine.Object.Destroy(_modelInstance);
                _modelInstance = null;
                InstantiateModel(FirePoint);
            }
            else if (_modelInstance != null)
            {
                _modelInstance.SetActive(true);
            }
        }

        public TurretPortRuntime(
            TurretPortConfig config,
            int index,
            Transform parent,
            PortInventorySettings invSettings,
            IObjectResolver resolver,
            PortAttributes templateAttributes = null,
            PortInventory boundInventory = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            PortIndex = index;
            PortId = $"{parent?.name ?? "Turret"}_Port{index}_{config.portName}";

            _resolver = resolver;
            IsLocked = config.isInitiallyLocked;

            var firePointGO = new GameObject(PortId);
            firePointGO.transform.SetParent(parent, worldPositionStays: false);
            FirePoint = firePointGO.transform;

            // 端口位置由 TurretPortManager.SetSphereCenter() 在运行时设置

            InstantiateModel(firePointGO.transform);

            // 绑定全局 PlayerLoadout 的 PortInventory(基地/战斗共享同一份装备数据)
            Inventory = boundInventory ?? new PortInventory(invSettings, index, templateAttributes);
        }

        #region 球冠定位

        /// <summary>
        /// 将端口对齐到球冠底面圆周上。
        /// Turret 在球冠顶点，port 按端口名称方向分布在球冠底面圆周。
        /// </summary>
        /// <param name="sphereCenter">星球中心</param>
        /// <param name="capHeight">球冠高度</param>
        public void AlignToSphere(Vector3 sphereCenter, float capHeight)
        {
            if (FirePoint == null) return;

            Vector3 turretPos = FirePoint.parent != null
                ? FirePoint.parent.position
                : FirePoint.position;

            TurretCapLayout.CalculatePortPose(
                sphereCenter,
                turretPos,
                capHeight,
                Config.portName,
                out Vector3 position);

            FirePoint.position = position;
        }

        #endregion

        #region 生命周期

        public void Dispose()
        {
            if (_modelInstance != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_modelInstance);
                else
                    UnityEngine.Object.DestroyImmediate(_modelInstance);
                _modelInstance = null;
            }

            if (FirePoint != null && FirePoint.gameObject != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(FirePoint.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(FirePoint.gameObject);
            }
        }

        #endregion

        #region 模型

        private void InstantiateModel(Transform parent)
        {
            GameObject prefab = IsLocked && Config.lockedModelPrefab != null
                ? Config.lockedModelPrefab
                : Config.portModelPrefab;

            if (prefab == null) return;

            _modelInstance = UnityEngine.Object.Instantiate(prefab, parent);
            _modelInstance.transform.localPosition = Vector3.zero;
            _modelInstance.transform.localRotation = Quaternion.identity;
            _modelInstance.SetActive(!IsLocked);
        }

        #endregion
    }
}
