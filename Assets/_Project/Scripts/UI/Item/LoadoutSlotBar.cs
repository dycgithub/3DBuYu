using System;
using System.Collections.Generic;
using TMPro;
using TurretSystem;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI.Item
{
    /// <summary>
    /// 装配区按钮条:1 个炮塔按钮 + N 个炮口按钮。
    /// 选择后通过 OnSlotSelected(slot) 广播(slot=-1 表示炮塔,>=0 为对应炮口索引)。
    /// 数据源为全局 PlayerLoadout,锁定状态来自 TurretBase 配置。
    /// </summary>
    public class LoadoutSlotBar : MonoBehaviour
    {
        public event Action<int> OnSlotSelected;

        [SerializeField] private GameObject _tabPrefab;
        [SerializeField] private Transform _tabContainer;
        [SerializeField] private Color _activeColor = Color.white;
        [SerializeField] private Color _lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private GameObject _lockedOverlayPrefab;

        private PlayerLoadout _loadout;
        private int _currentSlot;
        private readonly List<GameObject> _tabs = new();

        public void Initialize(PlayerLoadout loadout)
        {
            _loadout = loadout;
            _currentSlot = -1;
            GenerateTabs();
        }

        private void GenerateTabs()
        {
            ClearTabs();
            if (_loadout == null) return;

            int count = _loadout.PortInventories.Count + 1;

            var rect = (_tabContainer ?? transform) as RectTransform;
            float containerWidth = rect != null && rect.rect.width > 0 ? rect.rect.width : 580f;
            float hPadding = 4f;
            float wPadding = 4f;
            float slotW = (containerWidth - hPadding * 2f) / count;
            float slotH = 40f;

            AddTab("炮塔", -1, 0, slotW, slotH, wPadding);
            for (int i = 0; i < _loadout.PortInventories.Count; i++)
                AddTab($"P{i + 1}", i, i + 1, slotW, slotH, wPadding);

            SelectSlot(-1);
        }

        private void AddTab(string label, int slot, int index, float slotW, float slotH, float wPadding)
        {
            var tabGo = Instantiate(_tabPrefab, _tabContainer ?? transform);
            tabGo.name = slot < 0 ? "TurretTab" : $"PortTab_{slot}";

            var tabRect = tabGo.GetComponent<RectTransform>();
            if (tabRect != null)
            {
                tabRect.anchorMin = new Vector2(0f, 0.5f);
                tabRect.anchorMax = new Vector2(0f, 0.5f);
                tabRect.pivot = new Vector2(0f, 0.5f);
                tabRect.sizeDelta = new Vector2(slotW - wPadding * 2f, slotH);
                tabRect.anchoredPosition = new Vector2(wPadding + index * slotW, 0f);
            }

            var tmp = tabGo.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = label;
            if (tmp != null) tmp.fontSize = 14;

            var btn = tabGo.GetComponent<Button>() ?? tabGo.AddComponent<Button>();
            int captured = slot;
            btn.onClick.AddListener(() => SelectSlot(captured));

            if (slot >= 0 && _loadout.IsPortLocked(slot))
            {
                var image = tabGo.GetComponent<Image>();
                if (image != null) image.color = _lockedColor;

                if (_lockedOverlayPrefab != null)
                {
                    var overlay = Instantiate(_lockedOverlayPrefab, tabGo.transform);
                    overlay.name = "LockedOverlay";
                }

                btn.interactable = false;
            }

            _tabs.Add(tabGo);
        }

        public void SelectSlot(int slot)
        {
            if (_loadout == null) return;
            if (slot >= 0 && (slot >= _loadout.PortInventories.Count || _loadout.IsPortLocked(slot)))
                return;

            _currentSlot = slot;

            for (int i = 0; i < _tabs.Count; i++)
            {
                var image = _tabs[i].GetComponent<Image>();
                if (image != null)
                    image.color = (i == slot + 1) ? _activeColor : new Color(0.7f, 0.7f, 0.7f);
            }

            OnSlotSelected?.Invoke(slot);
        }

        public int CurrentSlot => _currentSlot;

        private void ClearTabs()
        {
            foreach (var tab in _tabs)
                if (tab != null) DestroyImmediate(tab);
            _tabs.Clear();
        }
    }
}