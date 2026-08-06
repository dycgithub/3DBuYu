using TurretSystem;
using UnityEngine;
using VContainer;
using _Project.UI.Inventory;

namespace _Project.UI.Item
{
    /// <summary>
    /// 背包面板中的装备区(炮塔 + 8 炮口装配按钮 + 单块网格切换)。
    /// 数据源为全局 PlayerLoadout,基地/战斗共享。
    /// 网格由统一 InventoryGridView 创建,保证所有网格大小与排列一致。
    /// </summary>
    public class ItemPanel : MonoBehaviour
    {
        [SerializeField] private InventoryGridView _turretGridPanel;
        [SerializeField] private InventoryGridView _portGridPanel;
        [SerializeField] private LoadoutSlotBar _portTabSwitcher;

        private PlayerLoadout _loadout;

        [Inject] private PlayerLoadout _injectedLoadout;

        public void Initialize()
        {
            if (_loadout == null)
                _loadout = _injectedLoadout;

            if (_loadout == null)
            {
                _loadout = ProjectLifetimeScope.Instance?.Container.Resolve<PlayerLoadout>();
            }

            if (_loadout == null) return;

            if (_turretGridPanel != null)
                _turretGridPanel.Initialize(_loadout.TurretInventory);

            if (_portTabSwitcher != null)
            {
                _portTabSwitcher.OnSlotSelected += HandleSlotSelected;
                _portTabSwitcher.Initialize(_loadout);
            }

            HandleSlotSelected(-1);
        }

        private void HandleSlotSelected(int slot)
        {
            if (_loadout == null) return;

            if (slot < 0)
            {
                if (_turretGridPanel != null)
                    _turretGridPanel.gameObject.SetActive(true);
                if (_portGridPanel != null)
                    _portGridPanel.gameObject.SetActive(false);
                return;
            }

            if (slot >= _loadout.PortInventories.Count) return;

            if (_portGridPanel != null)
            {
                _portGridPanel.Initialize(_loadout.PortInventories[slot]);
                _portGridPanel.gameObject.SetActive(true);
            }
            if (_turretGridPanel != null)
                _turretGridPanel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_portTabSwitcher != null)
                _portTabSwitcher.OnSlotSelected -= HandleSlotSelected;
        }
    }
}