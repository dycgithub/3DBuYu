using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI.Settings
{
    /// <summary>
    /// 常驻设置按钮(右上角),点击开合设置面板。
    /// </summary>
    public class SettingsButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private SettingsPanel _settingsPanel;

        private void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
        }

        private void Start()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _settingsPanel?.Toggle());
        }
    }
}
