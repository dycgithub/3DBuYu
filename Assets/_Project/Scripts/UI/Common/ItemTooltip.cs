using System.Collections.Generic;
using InventorySystem;
using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.Common
{
    public class ItemTooltip : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _displayNameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _typeText;
        [SerializeField] private TextMeshProUGUI _priceText;
        [SerializeField] private Transform _statRowContainer;
        [SerializeField] private GameObject _statRowPrefab;
        [SerializeField] private CanvasGroup _canvasGroup;

        private readonly List<GameObject> _statRows = new();

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            Hide();
        }

        public void Show(ItemConfig config, Vector2 screenPosition)
        {
            if (config == null) return;

            if (_displayNameText != null)
                _displayNameText.text = config.displayName;

            if (_descriptionText != null)
                _descriptionText.text = config.description;

            if (_typeText != null)
                _typeText.text = config.ItemType.ToString();

            if (_priceText != null)
            {
                var scope = ProjectLifetimeScope.Instance;
                var shop = scope?.Container?.Resolve<InventorySystem.Shop.ShopConfig>();
                int price = shop != null ? shop.GetBasePrice(config.itemId) : 0;
                _priceText.text = price > 0 ? $"价格: {price}" : "";
            }

            ClearStatRows();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = false;
            }

            transform.position = screenPosition;
        }

        public void Hide()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
            ClearStatRows();
        }

        private void ClearStatRows()
        {
            foreach (var row in _statRows)
                Destroy(row);
            _statRows.Clear();
        }
    }
}
