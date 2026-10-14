using System;
using GameSystem;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace _Project.Localization.Implement
{
    /// <summary>从现有设置存档选择语言；首次启动固定英文，不跟随系统语言。</summary>
    [Serializable]
    public sealed class SavedLocaleSelector : IStartupLocaleSelector
    {
        /// <summary>快速进入 Play Mode 会保留编辑器的空预览语言，因此在场景加载前显式应用存档。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeLanguage()
        {
            LocalizationSettings.SelectedLocale = new SavedLocaleSelector()
                .GetStartupLocale(LocalizationSettings.AvailableLocales);
        }

        /// <summary>将设置值限制为项目支持的语言。</summary>
        /// <param name="code">存档中的语言代码。</param>
        /// <returns>简体中文或英文代码。</returns>
        public static string Normalize(string code)
        {
            return code == "zh-Hans" ? "zh-Hans" : "en";
        }

        /// <summary>让 Localization 在初始化时读取玩家选择。</summary>
        /// <param name="availableLocales">官方包维护的可用语言。</param>
        /// <returns>已保存的语言，非法或缺失配置回退英文。</returns>
        public Locale GetStartupLocale(ILocalesProvider availableLocales)
        {
            return availableLocales.GetLocale(Normalize(SaveSystem.LoadSettings().languageCode));
        }
    }
}
