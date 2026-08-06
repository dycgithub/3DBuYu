using System.Collections.Generic;
using TMPro;
using TurretSystem;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI.Item
{
    /// <summary>
    /// 炮口 Tab 切换器。数据源为全局 PlayerLoadout,锁定状态来自 TurretBase 配置。
    /// </summary>
    public class PortTabSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject _tabPrefab;
        [SerializeField] private Transform _tabContainer;
        [SerializeField] private Color _activeColor = Color.white;
        [SerializeField] private Color _lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private GameObject _lockedOverlayPrefab;

        private PlayerLoadout _loadout;
        private int _currentPortIndex;
        private readonly List<GameObject> _tabs = new();

        private void GenerateTabs()
        {
            ClearTabs();
            if (_loadout == null) return;

            for (int i = 0; i < _loadout.PortInventories.Count; i++)
            {
                var tabGo = Instantiate(_tabPrefab, _tabContainer ?? transform);
                tabGo.name = $"PortTab_{i}";

                var tmp = tabGo.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = $"P{i + 1}";

                var btn = tabGo.GetComponent<Button>() ?? tabGo.AddComponent<Button>();
                int capturedIndex = i;
                btn.onClick.AddListener(() => SelectPort(capturedIndex));

                if (_loadout.IsPortLocked(i))
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
        }

        public void SelectPort(int index)
        {
            if (_loadout == null) return;
            if (index < 0 || index >= _loadout.PortInventories.Count) return;
            if (_loadout.IsPortLocked(index)) return;

            _currentPortIndex = index;

            for (int i = 0; i < _tabs.Count; i++)
            {
                var image = _tabs[i].GetComponent<Image>();
                if (image != null)
                    image.color = (i == index) ? _activeColor : new Color(0.7f, 0.7f, 0.7f);
            }
            
        }

        private void ClearTabs()
        {
            foreach (var tab in _tabs)
                DestroyImmediate(tab);
            _tabs.Clear();
        }
    }
}
