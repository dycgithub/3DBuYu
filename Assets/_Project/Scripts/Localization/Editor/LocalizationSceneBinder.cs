using System;
using System.Collections.Generic;
using System.Linq;
using _Project.Localization.View;
using _Project.UI.Card;
using _Project.UI.Common;
using _Project.UI.Settings;
using _Project.UI.Tutorial.View;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _Project.Localization.Editor
{
    /// <summary>编辑期绑定游戏界面和字体，保存场景与预制体；动态文本仍由各视图负责。</summary>
    internal static class LocalizationSceneBinder
    {
        private static readonly HashSet<string> DynamicResultNames = new()
        {
            "EyebrowLabel", "TitleLabel", "SubtitleLabel", "DifficultyLabel", "ElapsedTimeLabel",
            "KillCountLabel", "BestStreakLabel", "SessionPointsLabel", "SettlementMultiplierLabel",
            "SettlementFormulaLabel", "FailureRewardLabel"
        };

        /// <summary>绑定正式游戏资源；已打开场景使用内存中的编辑结果并保存，不关闭原场景。</summary>
        [MenuItem("Tools/Localization/Bind Game Interfaces")]
        private static void Bind()
        {
            var mapping = new Dictionary<string, string>();
            foreach (var row in LocalizationTableBuilder.ReadRows())
            {
                if (!row[1].Contains("{") && !row[0].StartsWith("item.") && !row[0].StartsWith("skill."))
                {
                    mapping.TryAdd(row[1], row[0]);
                    mapping.TryAdd(row[2], row[0]);
                }
            }

            mapping["RUN COMPLETE"] = "ui.runComplete";
            mapping["REWARD CONFIRMED"] = "ui.rewardConfirmed";
            mapping["RUN ENDED"] = "ui.runEnded";
            mapping["Energy"] = "ui.energy";
            mapping["Uses"] = "ui.uses";
            mapping["Cooldown"] = "ui.cooldown";
            var unmatched = new List<string>();
            var activeScene = SceneManager.GetActiveScene();
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Tests/"))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponentsInChildren<TMP_Text>(true).Length == 0)
                {
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    AddLanguageButtons(root);
                    count += BindRoot(root, mapping, unmatched, path);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        AddLanguageButtons(root);
                        count += BindRoot(root, mapping, unmatched, path);
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        throw new InvalidOperationException($"Could not save {path}");
                    }
                }
                finally
                {
                    if (!wasLoaded)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            SceneManager.SetActiveScene(activeScene);
            BindTutorials();
            AssetDatabase.SaveAssets();
            Debug.Log($"Localization: bound {count} static labels. Unmatched authored text:\n" + string.Join("\n", unmatched));
        }

        /// <summary>绑定静态文案的持久事件，并为全部 TMP 文本绑定字体表。</summary>
        private static int BindRoot(GameObject root, Dictionary<string, string> mapping, List<string> unmatched, string path)
        {
            var fonts = LocalizationEditorSettings.GetAssetTableCollection(LocalizationTableBuilder.FontTable);
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            int count = 0;
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.font != null && label.GetComponent<LocalizedFontEvent>() == null)
                {
                    string fontKey = LocalizationTableBuilder.AddFont(label.font, fonts, addressables);
                    var fontEvent = label.gameObject.AddComponent<LocalizedFontEvent>();
                    fontEvent.AssetReference = new LocalizedTmpFont
                    {
                        TableReference = LocalizationTableBuilder.FontTable,
                        TableEntryReference = fontKey
                    };
                    var setFont = (UnityAction<TMP_FontAsset>)Delegate.CreateDelegate(typeof(UnityAction<TMP_FontAsset>), label, "set_font");
                    UnityEventTools.AddPersistentListener(fontEvent.OnUpdateAsset, setFont);
                    fontEvent.OnUpdateAsset.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
                    EditorUtility.SetDirty(fontEvent);
                }

                bool isDynamic = IsDynamic(label);
                if (isDynamic)
                {
                    var staticEvent = label.GetComponent<LocalizeStringEvent>();
                    if (staticEvent != null)
                    {
                        UnityEngine.Object.DestroyImmediate(staticEvent);
                    }
                }

                if (string.IsNullOrWhiteSpace(label.text))
                {
                    continue;
                }

                if (isDynamic)
                {
                    if (mapping.TryGetValue(label.text, out string initialKey))
                    {
                        label.text = L10n.Get(initialKey);
                    }
                    else if (label.text.Any(c => c >= '\u4e00' && c <= '\u9fff'))
                    {
                        label.text = string.Empty;
                    }

                    EditorUtility.SetDirty(label);
                    continue;
                }

                var existing = label.GetComponent<LocalizeStringEvent>();
                if (existing != null)
                {
                    label.text = L10n.Get(existing.StringReference.TableEntryReference.Key);
                    existing.RefreshString();
                    EditorUtility.SetDirty(label);
                    count++;
                    continue;
                }

                if (mapping.TryGetValue(label.text, out string key))
                {
                    var localized = label.gameObject.AddComponent<LocalizeStringEvent>();
                    localized.StringReference = new LocalizedString(L10n.TableName, key);
                    var setText = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), label, "set_text");
                    UnityEventTools.AddPersistentListener(localized.OnUpdateString, setText);
                    localized.OnUpdateString.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
                    label.text = L10n.Get(key);
                    localized.RefreshString();
                    EditorUtility.SetDirty(localized);
                    EditorUtility.SetDirty(label);
                    count++;
                }
                else if (label.text.Any(c => c >= '\u4e00' && c <= '\u9fff'))
                {
                    unmatched.Add($"{path} :: {label.name} :: {label.text}");
                }
            }

            return count;
        }

        /// <summary>排除业务视图拥有的动态标签，避免静态事件覆盖商店费用、战局数值和物品内容。</summary>
        private static bool IsDynamic(TMP_Text label)
        {
            return label.GetComponentInParent<ItemTooltip>(true) != null ||
                label.GetComponentInParent<CardView>(true) != null ||
                label.GetComponentInParent<CardActionPanel>(true) != null ||
                label.GetComponent<PointsDisplayView>() != null ||
                IsShopCostText(label) ||
                label.transform.root.GetComponentsInChildren<GameSystem.SceneLoader>(true).Any(loader => loader.loadingText == label) ||
                IsTutorialText(label) ||
                (label.GetComponentInParent<_Project.UI.RunResult.View.RunResultPanel>(true) != null && DynamicResultNames.Contains(label.name)) ||
                label.name is "SupplyFiveDrawButtonLabel" or "SupplyDrawResultText" ||
                label.transform.parent?.name is "SupplyChestButton" or "SupplySlotMachineButton" or "SupplyMobiusButton" or "Language" or "FrameRate";
        }

        /// <summary>按序列化引用识别商店费用标签，费用参数由商店视图提供。</summary>
        /// <param name="label">待检查的文本组件。</param>
        /// <returns>文本是否由商店刷新视图管理。</returns>
        private static bool IsShopCostText(TMP_Text label)
        {
            var view = label.GetComponentInParent<InventorySystem.Shop.ShopRefreshView>(true);
            if (view == null)
            {
                return false;
            }

            var serialized = new SerializedObject(view);
            return serialized.FindProperty("costLabel").objectReferenceValue == label;
        }

        /// <summary>按序列化引用识别教程的动态标签，不依赖按钮子物体名称。</summary>
        private static bool IsTutorialText(TMP_Text label)
        {
            var view = label.GetComponentInParent<TutorialDialogView>(true);
            if (view == null)
            {
                return false;
            }

            var serialized = new SerializedObject(view);
            return new[] { "_title", "_description", "_stepCounter", "_nextLabel" }
                .Any(name => serialized.FindProperty(name).objectReferenceValue == label);
        }

        /// <summary>在设置面板中预置语言按钮；运行时只绑定引用，不创建界面。</summary>
        private static void AddLanguageButtons(GameObject root)
        {
            foreach (var panel in root.GetComponentsInChildren<OptionsPanel>(true))
            {
                Button language = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Language");
                if (language == null)
                {
                    var source = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "FrameRate")
                        ?? panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "BackGame");
                    if (source == null)
                    {
                        throw new InvalidOperationException($"No language button template on {panel.name}");
                    }

                    language = UnityEngine.Object.Instantiate(source, source.transform.parent);
                    language.name = "Language";
                    language.onClick = new Button.ButtonClickedEvent();
                    var rect = (RectTransform)language.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(320f, 44f);
                    rect.anchoredPosition = source.name == "BackGame" ? new Vector2(0f, -42f) : new Vector2(-320f, 105f);
                    foreach (var localized in language.GetComponentsInChildren<LocalizeStringEvent>(true))
                    {
                        UnityEngine.Object.DestroyImmediate(localized);
                    }

                    var text = language.GetComponentInChildren<TMP_Text>(true);
                    text.text = L10n.Get("settings.language");
                    text.fontSize = 24f;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 16f;
                    text.fontSizeMax = 24f;
                }

                var serialized = new SerializedObject(panel);
                serialized.FindProperty("_languageButton").objectReferenceValue = language;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>按资产路径和步骤索引配置教程翻译键，保留原推进规则。</summary>
        private static void BindTutorials()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/_Project/SO/Tutorial/StarterTutorial.asset");
            var serialized = new SerializedObject(asset);
            var steps = serialized.FindProperty("_steps");
            for (int index = 0; index < steps.arraySize; index++)
            {
                var step = steps.GetArrayElementAtIndex(index);
                step.FindPropertyRelative("_titleKey").stringValue = $"tutorial.starter.{index}.title";
                step.FindPropertyRelative("_descriptionKey").stringValue = $"tutorial.starter.{index}.description";
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
