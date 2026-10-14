using Services;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using VContainer;

namespace InventorySystem.Shop
{
    /// <summary>绑定场景中序列化的刷新按钮与费用文本，状态由商店、永久积分及拖拽服务决定。</summary>
    public sealed class ShopRefreshView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button refreshButton;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private GridView shopGrid;
        [Inject] private IShopService _shop;
        [Inject] private IPointsService _points;
        [Inject] private IInventoryDragState _drag;
        private bool _processing;

        /// <summary>只绑定已有 UI，不在运行时创建控件。</summary>
        private void Start()
        {
            refreshButton.onClick.AddListener(Refresh);
            LocalizationSettings.SelectedLocaleChanged += RefreshLanguage;
            RefreshLanguage(null);
            Update();
        }

        /// <summary>拖拽状态没有事件，按帧同步按钮；点击入口还会再次检查全部条件。</summary>
        private void Update()
        {
            if (refreshButton != null)
            {
                refreshButton.interactable = !_processing && _shop != null && !_shop.IsRefreshing &&
                    shopGrid != null && _drag != null && _drag.DraggingItem == null &&
                    _points != null && _points.HasEnoughPoints(ShopManager.RefreshCost);
            }
        }

        /// <summary>立即禁用按钮，交给商店准备、扣费和提交，完成后恢复状态。</summary>
        private void Refresh()
        {
            if (_processing || _shop == null)
            {
                return;
            }
            _processing = true;
            Update();
            try
            {
                _shop.TryRefreshShop(shopGrid, _drag);
            }
            finally
            {
                _processing = false;
                Update();
            }
        }

        /// <summary>语言切换时刷新费用模板，积分数值使用固定收费常量。</summary>
        private void RefreshLanguage(Locale locale)
        {
            costLabel.text = L10n.Get("shop.refreshCost", ShopManager.RefreshCost);
        }

        /// <summary>解除按钮及全局语言订阅，避免场景重载保留旧组件。</summary>
        private void OnDestroy()
        {
            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveListener(Refresh);
            }
            LocalizationSettings.SelectedLocaleChanged -= RefreshLanguage;
        }
    }
}
