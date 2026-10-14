using UnityEngine.Localization.Settings;

/// <summary>统一读取 GameText 翻译表；语言状态和带参数格式化均交给官方 Localization。</summary>
public static class L10n
{
    public const string TableName = "GameText";

    /// <summary>读取当前语言的文本，并按表中的模板格式化参数。</summary>
    /// <param name="key">稳定的翻译键。</param>
    /// <param name="arguments">模板参数。</param>
    /// <returns>当前语言文本；编辑器未选择预览语言时使用项目英文。</returns>
    public static string Get(string key, params object[] arguments)
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(
            TableName, key, LocalizationSettings.SelectedLocale ?? LocalizationSettings.ProjectLocale,
            arguments: arguments);
    }

    /// <summary>读取配置资产的翻译；临时测试物品和未配置翻译的资产保留其原始名称。</summary>
    /// <param name="key">物品或教程的稳定翻译键。</param>
    /// <param name="fallback">临时配置的原始文本。</param>
    /// <returns>有翻译时返回译文，否则返回原始文本。</returns>
    public static string GetOrDefault(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key) || !LocalizationSettings.HasSettings)
        {
            return fallback;
        }

        var locale = LocalizationSettings.SelectedLocale ?? LocalizationSettings.ProjectLocale;
        var table = LocalizationSettings.StringDatabase.GetTable(TableName, locale);
        return table?.GetEntry(key)?.GetLocalizedString() ?? fallback;
    }
}
