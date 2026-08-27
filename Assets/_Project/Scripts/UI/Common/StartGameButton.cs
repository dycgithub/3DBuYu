using System.Collections.Generic;
using GameSystem;
using Services;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.Common
{
    /// <summary>
    /// 开始战斗按钮:运行时查找唯一的 SceneLoader(根容器 DDOL 实例)并绑定点击。
    /// 根容器改由 VContainerSettings.RootLifetimeScope 管理后,SceneLoader 不再位于 UIScene 场景内,
    /// 无法再用序列化引用绑定,故改为运行时解析(此时全局有且仅有一个 SceneLoader,查找唯一)。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class StartGameButton : MonoBehaviour
    {
        [SerializeField] private GridView _storageForShop;

        [Inject] private SceneLoader _sceneLoader;
        [Inject] private IInventoryTransferStorage _inventoryTransferStorage;
        private bool _started;

        private void Awake()
        {
            var button = GetComponent<Button>();
            button.onClick.RemoveAllListeners(); // 清掉旧的序列化引用(原指向场景内 SceneLoader,已失效)
            button.onClick.AddListener(StartBattle);
        }

        private void StartBattle()
        {
            if (_started)
                return;

            if (_sceneLoader == null)
            {
                Debug.LogWarning("[StartGameButton] 找不到 SceneLoader,请确认 VContainerSettings.RootLifetimeScope 已配置。", this);
                return;
            }

            if (_inventoryTransferStorage == null)
            {
                Debug.LogWarning("[StartGameButton] 临时物品存储未注入,无法开始战斗。", this);
                return;
            }

            if (_storageForShop == null)
            {
                Debug.LogWarning("[StartGameButton] 未绑定 StorageForShop,无法开始战斗。", this);
                return;
            }

            if (_storageForShop.GridType != GridType.StorageForShop)
            {
                Debug.LogWarning(
                    $"[StartGameButton] 绑定的网格类型错误: {_storageForShop.GridType},需要 StorageForShop。",
                    this);
                return;
            }

            _storageForShop.EnsureGridVM();
            var snapshots = new List<InventoryItemSnapshot>(_storageForShop.ItemCount);
            foreach (ItemVM item in _storageForShop.Items)
                snapshots.Add(new InventoryItemSnapshot(item));

            _inventoryTransferStorage.Replace(snapshots);
            _storageForShop.ClearAll();
            _started = true;

            _sceneLoader.LoadGameScene();
        }
    }
}
