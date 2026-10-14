using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace _Project.UI.Settings
{
    /// <summary>绑定编辑期预置的语言按钮，并在语言切换后刷新设置动态文本。</summary>
    public partial class OptionsPanel
    {
        [Header("语言")]
        [SerializeField] private Button _languageButton;

        /// <summary>连接语言按钮和官方语言变化事件。</summary>
        private void BindLanguage()
        {
            _languageButton ??= FindComponent<Button>("Language");
            _languageButton?.onClick.AddListener(ToggleLanguage);
            LocalizationSettings.SelectedLocaleChanged += RefreshLanguage;
            RefreshLanguage(LocalizationSettings.SelectedLocale);
        }

        /// <summary>释放按钮和语言事件，避免跨场景残留监听。</summary>
        private void UnbindLanguage()
        {
            _languageButton?.onClick.RemoveListener(ToggleLanguage);
            LocalizationSettings.SelectedLocaleChanged -= RefreshLanguage;
        }

        /// <summary>在英文和简体中文之间切换，由设置服务持久化。</summary>
        private void ToggleLanguage()
        {
            string next = LocalizationSettings.SelectedLocale?.Identifier.Code == "zh-Hans" ? "en" : "zh-Hans";
            optionsService?.SetLanguage(LocalizationSettings.AvailableLocales.GetLocale(next));
        }

        /// <summary>刷新按钮、帧率和重绑定提示，不改变设置或输入状态。</summary>
        /// <param name="_">官方包当前选中的语言。</param>
        private void RefreshLanguage(Locale _)
        {
            TMP_Text label = _languageButton != null ? _languageButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null)
            {
                label.text = L10n.Get("settings.language");
            }

            RefreshFrameRateLabel();
            if (_isRebinding && _bindingButtons.TryGetValue(_activeBinding, out Button button))
            {
                SetBindingLabel(button, L10n.Get("settings.pressKey"));
            }
        }
    }
}
