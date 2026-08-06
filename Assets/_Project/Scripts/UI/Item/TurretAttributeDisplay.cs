using TMPro;
using TurretSystem;
using UnityEngine;

namespace _Project.UI.Item
{
    public class TurretAttributeDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statsText;

        private TurretInventory _turretInventory;

        public void Initialize(TurretInventory inventory)
        {
            _turretInventory = inventory;
            if (_turretInventory != null)
            {
                _turretInventory.OnInventoryChanged += UpdateDisplay;
                UpdateDisplay();
            }
        }

        private void OnDestroy()
        {
            if (_turretInventory != null)
                _turretInventory.OnInventoryChanged -= UpdateDisplay;
        }

        private void UpdateDisplay()
        {
            if (_statsText != null && _turretInventory?.Attributes != null)
                _statsText.text = _turretInventory.Attributes.GetDescription();
        }
    }
}
