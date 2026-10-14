using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Project.Localization.Implement;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace _Project.Localization.Editor
{
    /// <summary>将可审阅的中英文 TSV 导入官方翻译表，配置固定英文启动和字体资产表。</summary>
    internal static class LocalizationTableBuilder
    {
        internal const string Folder = "Assets/_Project/Localization";
        internal const string FontTable = "GameFonts";
        internal const string ChineseFontPath = "Assets/_Project/Art/FontsAssets/SmileySans-Oblique SDF.asset";

        /// <summary>更新翻译表和语言设置；重复执行不会创建重复语言或翻译键。</summary>
        [MenuItem("Tools/Localization/Import Game Translations")]
        private static void Import()
        {
            var addressables = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, Folder + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            foreach (string code in new[] { "en", "zh-Hans" })
            {
                if (LocalizationEditorSettings.GetLocales().All(l => l.Identifier.Code != code))
                {
                    var locale = Locale.CreateLocale(code);
                    AssetDatabase.CreateAsset(locale, $"{Folder}/{code}.asset");
                    LocalizationEditorSettings.AddLocale(locale);
                }
            }

            LocalizationSettings.StartupLocaleSelectors.Clear();
            LocalizationSettings.StartupLocaleSelectors.Add(new SavedLocaleSelector());
            LocalizationSettings.ProjectLocale = LocalizationEditorSettings.GetLocales().First(l => l.Identifier.Code == "en");
            LocalizationSettings.InitializeSynchronously = true;
            EditorUtility.SetDirty(settings);

            var collection = LocalizationEditorSettings.GetStringTableCollection(L10n.TableName)
                ?? LocalizationEditorSettings.CreateStringTableCollection(L10n.TableName, Folder);
            foreach (var row in ReadRows())
            {
                foreach (var table in collection.StringTables)
                {
                    string value = table.LocaleIdentifier.Code == "en" ? row[1] : row[2];
                    var entry = table.GetEntry(row[0]) ?? table.AddEntry(row[0], value);
                    entry.Value = value;
                    EditorUtility.SetDirty(table);
                }
            }

            collection.SetPreloadTableFlag(true);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);

            var fonts = LocalizationEditorSettings.GetAssetTableCollection(FontTable)
                ?? LocalizationEditorSettings.CreateAssetTableCollection(FontTable, Folder);
            var chinese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
            chinese.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            chinese.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(chinese);
            AddFont(chinese, fonts, addressables);
            AssetDatabase.SaveAssets();
            Debug.Log($"Localization: imported {collection.SharedData.Entries.Count} bilingual keys.");
        }

        /// <summary>读取三列翻译数据，转义换行恢复为真正换行。</summary>
        /// <returns>翻译键、英文、中文行。</returns>
        internal static IEnumerable<string[]> ReadRows()
        {
            foreach (string line in File.ReadLines(Folder + "/GameText.tsv").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] columns = line.Split('\t');
                if (columns.Length != 3 || columns.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidOperationException($"Invalid localization row: {line}");
                }

                yield return columns.Select(s => s.Replace("\\n", "\n")).ToArray();
            }
        }

        /// <summary>保留每种英文原字体，并统一绑定项目已有的中文动态字体。</summary>
        /// <param name="english">文本原本配置的字体。</param>
        /// <param name="collection">字体资产表。</param>
        /// <param name="addressables">官方可寻址资源配置。</param>
        /// <returns>字体表中的稳定键。</returns>
        internal static string AddFont(TMP_FontAsset english, AssetTableCollection collection, AddressableAssetSettings addressables)
        {
            string path = AssetDatabase.GetAssetPath(english);
            if (path.Contains("/Resources/"))
            {
                string destination = $"{Folder}/{english.name}.asset";
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destination) == null)
                {
                    AssetDatabase.CopyAsset(path, destination);
                }

                path = destination;
            }

            string englishGuid = AssetDatabase.AssetPathToGUID(path);
            string chineseGuid = AssetDatabase.AssetPathToGUID(ChineseFontPath);
            string key = "font." + englishGuid;
            addressables.CreateOrMoveEntry(englishGuid, addressables.DefaultGroup);
            addressables.CreateOrMoveEntry(chineseGuid, addressables.DefaultGroup);
            foreach (var table in collection.AssetTables)
            {
                string guid = table.LocaleIdentifier.Code == "en" ? englishGuid : chineseGuid;
                var entry = table.GetEntry(key) ?? table.AddEntry(key, guid);
                entry.Guid = guid;
                EditorUtility.SetDirty(table);
            }

            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            return key;
        }
    }
}
