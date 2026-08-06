using UnityEngine;

namespace _Project.UI.Inventory
{
    /// <summary>
    /// 背包组合面板入口:仓库/商店/装备网格的容器。
    /// 由 SettingsPanel 的"背包"按钮调用 SetVisible 打开/关闭。
    /// 具体网格由 InventoryGridView 统一构建,本类只负责容器显隐。
    /// </summary>
    public class InventoryCompoundPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;

        private void Awake()
        {
            if (_root == null) _root = gameObject;
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
                _root.SetActive(visible);
        }

        public void Toggle()
        {
            if (_root != null)
                _root.SetActive(!_root.activeSelf);
        }
    }
}
