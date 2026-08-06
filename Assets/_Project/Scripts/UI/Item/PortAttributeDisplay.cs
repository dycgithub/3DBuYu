using TMPro;
using TurretSystem;
using UnityEngine;

namespace _Project.UI.Item
{
    public class PortAttributeDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statsText;

        private PortInventory _portInventory;

        public void Initialize(PortInventory inventory)
        {
            _portInventory = inventory;
            if (_portInventory != null)
            {
                _portInventory.OnInventoryChanged += UpdateDisplay;
                UpdateDisplay();
            }
        }

        private void OnDestroy()
        {
            if (_portInventory != null)
                _portInventory.OnInventoryChanged -= UpdateDisplay;
        }

        private void UpdateDisplay()
        {
            if (_statsText != null && _portInventory?.Attributes != null)
                _statsText.text = _portInventory.Attributes.GetDescription();
        }
    }
}
