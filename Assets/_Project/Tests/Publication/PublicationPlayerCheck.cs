#if PUBLICATION_CHECKS && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameSystem;
using Supply;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using VContainer;

namespace _Project.Tests.Publication
{
    /// <summary>仅在专用验证构建中执行真实场景与本地化检查，不参与普通发布包。</summary>
    internal sealed class PublicationPlayerCheck : MonoBehaviour
    {
        private const float TimeoutSeconds = 180f;
        private const string FontKey = "font.bb235162a49178c40b1ded37613bad4f";
        private readonly List<string> _cases = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();
        private readonly HashSet<string> _locations = new();
        private string _output;
        private string _startupLocale;
        private SceneLoader _loader;
        private OptionsService _options;
        private bool _finished;

        /// <summary>只有显式传入检查参数时创建验证对象，避免改变正常启动流程。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (!Environment.GetCommandLineArgs().Contains("-publication-check"))
            {
                return;
            }

            var root = new GameObject("PublicationPlayerCheck");
            DontDestroyOnLoad(root);
            root.AddComponent<PublicationPlayerCheck>();
        }

        /// <summary>记录 Player 错误，并依次检查主菜单、战斗和补给的中英文显示。</summary>
        private IEnumerator Start()
        {
            _output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Verification");
            Directory.CreateDirectory(_output);
            Application.logMessageReceived += HandleLog;
            yield return LocalizationSettings.InitializationOperation;
            yield return null;

            _startupLocale = LocalizationSettings.SelectedLocale.Identifier.Code;
            string[] arguments = Environment.GetCommandLineArgs();
            int expectedIndex = Array.IndexOf(arguments, "-expected-locale");
            Require(expectedIndex >= 0 && expectedIndex + 1 < arguments.Length,
                "必须指定预期启动语言。");
            Require(_startupLocale == arguments[expectedIndex + 1], "启动语言必须恢复本地存档。");
            _cases.Add("startup locale: " + _startupLocale);

            ProjectLifetimeScope scope = FindFirstObjectByType<ProjectLifetimeScope>();
            Require(scope != null && scope.Container != null, "全局 VContainer 必须成功初始化。");
            _options = scope.Container.Resolve<OptionsService>();
            _loader = scope.Container.Resolve<SceneLoader>();
            Time.timeScale = 0f;
            Require(SceneManager.GetSceneByName("UIScene").isLoaded, "首场景必须为 UIScene。");
            yield return CheckLanguages("UIScene");

            _loader.LoadGameScene();
            yield return WaitForScene("GameScene");
            Require(!SceneManager.GetSceneByName("UIScene").isLoaded, "进入战斗后主菜单必须卸载。");
            yield return CheckLanguages("GameScene");

            Require(_loader.TryLoadSupplyScene(SupplyMethod.Chest), "补给入口必须成功启动加载。");
            yield return WaitForScene("SupplyScene");
            Require(SceneManager.GetSceneByName("GameScene").isLoaded, "补给期间必须保留战斗场景。");
            Require(RenderSettings.skybox == null, "补给场景不得重新加载未使用的天空盒。");
            yield return CheckLanguages("SupplyScene");

            Require(_loader.TryReturnToGameScene(), "补给返回必须成功启动。");
            while (SceneManager.GetSceneByName("SupplyScene").isLoaded)
            {
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1f);
            Require(SceneManager.GetActiveScene().name == "GameScene", "返回后必须恢复战斗场景。");
            _cases.Add("SupplyScene -> GameScene");

            _loader.ReturnToMainMenu();
            yield return WaitForScene("UIScene");
            Require(!SceneManager.GetSceneByName("GameScene").isLoaded, "返回主菜单后必须卸载战斗。");
            _cases.Add("GameScene -> UIScene");
            CheckLocalLocations();
            Require(SaveSystem.LoadSettings().languageCode == "zh-Hans", "切换后的语言必须写入本地设置。");
            Finish(_errors.Count == 0);
        }

        /// <summary>等待真实加载流程完成并让场景 Start 与 UI 绑定执行。</summary>
        /// <param name="sceneName">预期激活的场景名。</param>
        private IEnumerator WaitForScene(string sceneName)
        {
            while (!SceneManager.GetSceneByName(sceneName).isLoaded ||
                   SceneManager.GetActiveScene().name != sceneName)
            {
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1f);
            _cases.Add("active scene: " + sceneName);
        }

        /// <summary>通过实际设置服务切换语言，读取本地表、字体并捕获可见 UI。</summary>
        /// <param name="sceneName">截图和检查记录对应的正式场景。</param>
        private IEnumerator CheckLanguages(string sceneName)
        {
            foreach (string code in new[] { "en", "zh-Hans" })
            {
                var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                Require(locale != null, "必须包含语言：" + code);
                _options.SetLanguage(locale);
                var table = LocalizationSettings.StringDatabase.GetTableAsync("GameText", locale);
                yield return table;
                Require(table.Status == AsyncOperationStatus.Succeeded && table.Result != null,
                    "本地翻译表必须加载成功：" + code);
                Require(L10n.Get("ui.settings") == (code == "en" ? "Settings" : "设置"),
                    "设置文本必须正确翻译：" + code);
                Require(L10n.Get("item.002.name") == (code == "en" ? "Heavy Warhead" : "强力弹头"),
                    "动态物品名称必须正确翻译：" + code);
                var font = LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<TMP_FontAsset>(
                    "GameFonts", FontKey, locale);
                yield return font;
                Require(font.Status == AsyncOperationStatus.Succeeded && font.Result != null,
                    "本地字体表必须加载成功：" + code);
                Require(font.Result.HasCharacters("Settings设置强力弹头", out uint[] missing, true, true),
                    "字体及回退字体必须包含验证字符：" + code);
                yield return new WaitForSecondsRealtime(1f);
                foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                {
                    Require(text.font != null, "可见 TMP 文本必须绑定字体：" + text.name);
                }
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(_output, sceneName + "-" + code + ".png"));
                yield return new WaitForSecondsRealtime(0.5f);
                _cases.Add(sceneName + ": " + code + " text/font loaded");
            }
        }

        /// <summary>核对 Catalog 中所有翻译表和字体的依赖路径，不允许远程 HTTP 资源。</summary>
        private void CheckLocalLocations()
        {
            foreach (var locator in Addressables.ResourceLocators)
            {
                foreach (object key in locator.Keys)
                {
                    if (locator.Locate(key, null, out var locations))
                    {
                        foreach (var location in locations)
                        {
                            CollectLocation(location);
                        }
                    }
                }
            }
            Require(_locations.Any(path => path.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)),
                "验证必须覆盖实际的本地 Bundle。");
            _cases.Add("catalog dependencies contain no HTTP resources");
        }

        /// <summary>去重遍历资源位置和依赖，避免循环依赖导致递归不结束。</summary>
        /// <param name="location">Addressables 解析出的资源位置。</param>
        private void CollectLocation(UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation location)
        {
            if (!_locations.Add(location.InternalId))
            {
                return;
            }
            Require(!location.InternalId.StartsWith("http", StringComparison.OrdinalIgnoreCase),
                "发布资源不得依赖网络：" + location.InternalId);
            if (location.HasDependencies)
            {
                foreach (var dependency in location.Dependencies)
                {
                    CollectLocation(dependency);
                }
            }
        }

        /// <summary>任何 Player 错误都使验证失败，警告记录但不自动判失败。</summary>
        /// <param name="message">日志正文。</param>
        /// <param name="stackTrace">调用堆栈。</param>
        /// <param name="type">日志类别。</param>
        private void HandleLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors.Add(message + "\n" + stackTrace);
                Finish(false);
            }
            else if (type == LogType.Warning)
            {
                _warnings.Add(message);
            }
        }

        /// <summary>给加载失败或协程异常提供有限等待，避免无人值守验证永久挂起。</summary>
        private void Update()
        {
            if (!_finished && Time.realtimeSinceStartup > TimeoutSeconds)
            {
                _errors.Add("Publication check timed out.");
                Finish(false);
            }
        }

        /// <summary>断言失败时交给 Unity 的异常日志处理统一生成失败报告。</summary>
        /// <param name="condition">必须成立的发布行为。</param>
        /// <param name="message">失败原因。</param>
        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>写出结构化证据后退出；结果文件位于验证游戏目录内。</summary>
        /// <param name="passed">测试是否通过。</param>
        private void Finish(bool passed)
        {
            if (_finished)
            {
                return;
            }
            _finished = true;
            Application.logMessageReceived -= HandleLog;
            File.WriteAllText(Path.Combine(_output, "result.json"), JsonUtility.ToJson(new CheckResult
            {
                passed = passed,
                startupLocale = _startupLocale,
                checks = _cases.ToArray(),
                errors = _errors.ToArray(),
                warnings = _warnings.ToArray(),
                resourceLocations = _locations.OrderBy(path => path).ToArray()
            }, true));
            Application.Quit(passed ? 0 : 1);
        }

        /// <summary>保存真实 Player 中的检查结果与资源路径。</summary>
        [Serializable]
        private sealed class CheckResult
        {
            public bool passed;
            public string startupLocale;
            public string[] checks;
            public string[] errors;
            public string[] warnings;
            public string[] resourceLocations;
        }
    }
}
#endif
