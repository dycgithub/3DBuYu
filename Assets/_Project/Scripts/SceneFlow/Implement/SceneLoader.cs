using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Supply;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using VContainer;
using UIDocument = UnityEngine.UIElements.UIDocument;

namespace GameSystem
{
    /// <summary>
    /// 管理基地、战斗和 SupplyScene 的异步切换，以及当前语言的加载进度。
    /// <para>
    /// UIScene 与 GameScene 仍采用互斥加载；SupplyScene 则以 Additive 方式暂时叠加在
    /// GameScene 上，保留原战斗对象和状态，返回时再卸载 SupplyScene。
    /// </para>
    /// </summary>
    public partial class SceneLoader : MonoBehaviour
    {
        [Header("Loading UI")]
        public GameObject loadingCanvas;
        public Slider progressBar;
        public TextMeshProUGUI loadingText;

        [Header("场景名称")]
        public string mainMenuScene = "UIScene";
        public string gameScene = "GameScene";
        [SerializeField] private string _supplyScene = "SupplyScene";

        [Header("启动行为")]
        public bool autoLoadOnStart = true;

        [Inject] private AudioManager _audioManager;

        private readonly List<(Behaviour Component, bool WasEnabled)> _suspendedGameComponents = new();
        private readonly List<(Renderer Component, bool WasEnabled)> _suspendedGameRenderers = new();
        private string _currentGameScene;
        private SupplyMethod _pendingSupplyMethod = SupplyMethod.Chest;
        private bool _isTransitioning;

        /// <summary>
        /// 场景切换完成后触发，参数为切换完成后的场景名称。
        /// </summary>
        public event Action<string> SceneChanged;

        /// <summary>SupplyScene 成功加载后触发，参数为本次抽奖入口。</summary>
        public event Action<SupplyMethod> SupplySceneLoaded;

        /// <summary>SupplyScene 加载失败并恢复 GameScene 后触发。</summary>
        public event Action<SupplyMethod> SupplySceneLoadFailed;

        /// <summary>隐藏加载界面，订阅场景加载和语言变化。</summary>
        private void Awake()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LocalizationSettings.SelectedLocaleChanged += RefreshLoadingLanguage;
            SetLoadingVisible(false);
        }

        /// <summary>根对象销毁时解除场景加载和语言变化事件。</summary>
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LocalizationSettings.SelectedLocaleChanged -= RefreshLoadingLanguage;
        }

        /// <summary>按配置决定是否在启动时加载战斗场景。</summary>
        private void Start()
        {
            PlaySceneBgm(SceneManager.GetActiveScene().name);

            if (autoLoadOnStart)
            {
                LoadGameScene();
            }
        }

        /// <summary>以 Additive 模式加载战斗场景，并卸载主菜单场景。</summary>
        public void LoadGameScene()
        {
            if (_isTransitioning || SceneManager.GetSceneByName(gameScene).isLoaded)
            {
                return;
            }

            _isTransitioning = true;
            StartCoroutine(LoadSceneAdditive(gameScene));
        }

        /// <summary>
        /// 请求进入 SupplyScene，并保存本次选择的入口类型。
        /// </summary>
        /// <param name="method">SupplyScene 的入口类型。</param>
        /// <returns>请求成功启动时返回 true；当前状态不允许切换时返回 false。</returns>
        public bool TryLoadSupplyScene(SupplyMethod method)
        {
            if (_isTransitioning || !Enum.IsDefined(typeof(SupplyMethod), method) ||
                !SceneManager.GetSceneByName(gameScene).isLoaded ||
                SceneManager.GetSceneByName(_supplyScene).isLoaded ||
                !Application.CanStreamedLevelBeLoaded(_supplyScene))
            {
                return false;
            }

            _pendingSupplyMethod = method;
            _isTransitioning = true;
            StartCoroutine(LoadSupplySceneAdditive());
            return true;
        }

        /// <summary>
        /// 请求卸载 SupplyScene 并恢复原有 GameScene。
        /// </summary>
        /// <returns>请求成功启动时返回 true；当前状态不允许返回时返回 false。</returns>
        public bool TryReturnToGameScene()
        {
            if (_isTransitioning || !SceneManager.GetSceneByName(_supplyScene).isLoaded ||
                !SceneManager.GetSceneByName(gameScene).isLoaded)
            {
                return false;
            }

            _isTransitioning = true;
            StartCoroutine(UnloadSupplyScene());
            return true;
        }

        /// <summary>
        /// 读取并清除本次 SupplyScene 入口类型，避免下一次进入沿用旧选择。
        /// </summary>
        /// <returns>本次进入 SupplyScene 应激活的入口类型。</returns>
        public SupplyMethod ConsumeSupplyMethod()
        {
            SupplyMethod method = _pendingSupplyMethod;
            _pendingSupplyMethod = SupplyMethod.Chest;
            return method;
        }

        /// <summary>卸载当前战斗场景，并重新加载主菜单场景。</summary>
        public void ReturnToMainMenu()
        {
            if (_isTransitioning || SceneManager.GetSceneByName(_supplyScene).isLoaded)
            {
                return;
            }

            if (string.IsNullOrEmpty(_currentGameScene))
            {
                if (!SceneManager.GetSceneByName(gameScene).isLoaded)
                {
                    return;
                }

                _currentGameScene = gameScene;
            }

            _isTransitioning = true;
            StartCoroutine(UnloadGameScene());
        }

        /// <summary>执行战斗场景的异步加载、激活和主菜单卸载流程。</summary>
        /// <param name="sceneName">要加载的战斗场景名称。</param>
        private IEnumerator LoadSceneAdditive(string sceneName)
        {
            SetLoadingVisible(true);

            Scene uiScene = SceneManager.GetSceneByName(mainMenuScene);
            SetEventSystemsEnabled(uiScene, false);

            AsyncOperation sceneLoadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            sceneLoadOperation.allowSceneActivation = false;

            while (sceneLoadOperation.progress < 0.9f)
            {
                UpdateLoadingBar(sceneLoadOperation.progress);
                yield return null;
            }

            UpdateLoadingBar(1f);
            sceneLoadOperation.allowSceneActivation = true;
            while (!sceneLoadOperation.isDone)
            {
                yield return null;
            }

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                SceneManager.SetActiveScene(loadedScene);
            }

            if (uiScene.isLoaded)
            {
                AsyncOperation menuUnloadOperation = SceneManager.UnloadSceneAsync(uiScene);
                while (menuUnloadOperation != null && !menuUnloadOperation.isDone)
                {
                    yield return null;
                }
            }

            SetLoadingVisible(false);
            _currentGameScene = sceneName;
            _isTransitioning = false;
            SceneChanged?.Invoke(sceneName);
        }

        /// <summary>
        /// 保留 GameScene 的对象和战斗状态，仅关闭其表现与输入后加载 SupplyScene。
        /// </summary>
        private IEnumerator LoadSupplySceneAdditive()
        {
            SetLoadingVisible(true);

            SupplyMethod method = _pendingSupplyMethod;
            Scene gameSceneObject = SceneManager.GetSceneByName(gameScene);
            SuspendGameScene(gameSceneObject);
            // 让入口控制器先完成按钮锁定和战斗暂停，避免失败事件在 TryLoad 返回前触发。
            yield return null;

            AsyncOperation sceneLoadOperation = SceneManager.LoadSceneAsync(
                _supplyScene,
                LoadSceneMode.Additive);
            if (sceneLoadOperation == null)
            {
                FailSupplySceneLoad(method);
                yield break;
            }

            sceneLoadOperation.allowSceneActivation = false;
            while (sceneLoadOperation.progress < 0.9f)
            {
                UpdateLoadingBar(sceneLoadOperation.progress);
                yield return null;
            }

            UpdateLoadingBar(1f);
            sceneLoadOperation.allowSceneActivation = true;
            while (!sceneLoadOperation.isDone)
            {
                yield return null;
            }

            Scene supplySceneObject = SceneManager.GetSceneByName(_supplyScene);
            if (!supplySceneObject.IsValid() || !supplySceneObject.isLoaded)
            {
                FailSupplySceneLoad(method);
                yield break;
            }

            SceneManager.SetActiveScene(supplySceneObject);
            SetEventSystemsEnabled(supplySceneObject, true);
            SetLoadingVisible(false);
            _isTransitioning = false;
            SupplySceneLoaded?.Invoke(method);
            SceneChanged?.Invoke(_supplyScene);
        }

        /// <summary>恢复 GameScene 并报告 SupplyScene 加载失败。</summary>
        /// <param name="method">本次加载失败的抽奖入口。</param>
        private void FailSupplySceneLoad(SupplyMethod method)
        {
            RestoreGameScene();
            SetLoadingVisible(false);
            _isTransitioning = false;
            _pendingSupplyMethod = SupplyMethod.Chest;
            SupplySceneLoadFailed?.Invoke(method);
        }

        /// <summary>关闭 SupplyScene 的表现，恢复 GameScene 后卸载 SupplyScene。</summary>
        private IEnumerator UnloadSupplyScene()
        {
            SetLoadingVisible(true);

            Scene supplySceneObject = SceneManager.GetSceneByName(_supplyScene);
            SetEventSystemsEnabled(supplySceneObject, false);
            DisableSceneComponents<Camera>(supplySceneObject);
            DisableSceneComponents<CinemachineBrain>(supplySceneObject);
            DisableSceneComponents<AudioListener>(supplySceneObject);
            DisableSceneComponents<Light>(supplySceneObject);
            DisableSceneComponents<Canvas>(supplySceneObject);

            Scene gameSceneObject = SceneManager.GetSceneByName(gameScene);
            if (gameSceneObject.IsValid() && gameSceneObject.isLoaded)
            {
                SceneManager.SetActiveScene(gameSceneObject);
                RestoreGameScene();
            }

            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(_supplyScene);
            while (unloadOperation != null && !unloadOperation.isDone)
            {
                yield return null;
            }

            SetLoadingVisible(false);
            _isTransitioning = false;
            SceneChanged?.Invoke(gameScene);
        }

        /// <summary>执行战斗场景卸载和主菜单场景加载流程。</summary>
        private IEnumerator UnloadGameScene()
        {
            SetLoadingVisible(true);

            Scene mainMenu = SceneManager.GetSceneByName(mainMenuScene);
            if (!mainMenu.isLoaded)
            {
                AsyncOperation mainMenuLoadOperation =
                    SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Additive);
                while (mainMenuLoadOperation != null && !mainMenuLoadOperation.isDone)
                {
                    yield return null;
                }

                mainMenu = SceneManager.GetSceneByName(mainMenuScene);
            }

            if (mainMenu.IsValid() && mainMenu.isLoaded)
            {
                SceneManager.SetActiveScene(mainMenu);
                SetEventSystemsEnabled(mainMenu, true);
            }

            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(_currentGameScene);
            while (unloadOperation != null && !unloadOperation.isDone)
            {
                yield return null;
            }

            _currentGameScene = null;
            SetLoadingVisible(false);
            _isTransitioning = false;
            SceneChanged?.Invoke(mainMenuScene);
        }

        /// <summary>在新场景的 Start 执行前切换 Active Scene，保证池实例归属正确。</summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                (scene.name != gameScene &&
                 scene.name != mainMenuScene &&
                 scene.name != _supplyScene))
            {
                return;
            }

            if (scene.name == _supplyScene)
            {
                SetSupplyBrainUnscaled(scene);
            }

            SceneManager.SetActiveScene(scene);
            PlaySceneBgm(scene.name);
        }

        /// <summary>按进入的主场景向跨场景音频管理器请求对应背景音乐。</summary>
        /// <param name="sceneName">已激活场景的名称。</param>
        private void PlaySceneBgm(string sceneName)
        {
            if (sceneName == mainMenuScene)
            {
                _audioManager.PlayBGM(BGMType.Menu);
            }
            else if (sceneName == gameScene)
            {
                _audioManager.PlayBGM(BGMType.Game);
            }
        }

        /// <summary>让 SupplyScene 的镜头混合在全局暂停期间继续更新。</summary>
        private static void SetSupplyBrainUnscaled(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                CinemachineBrain[] brains =
                    roots[rootIndex].GetComponentsInChildren<CinemachineBrain>(true);
                for (int brainIndex = 0; brainIndex < brains.Length; brainIndex++)
                {
                    brains[brainIndex].m_IgnoreTimeScale = true;
                }
            }
        }

        /// <summary>
        /// 保存 GameScene 的表现和输入组件状态，并临时关闭它们。
        /// </summary>
        /// <param name="scene">需要暂时隐藏的 GameScene。</param>
        private void SuspendGameScene(Scene scene)
        {
            _suspendedGameComponents.Clear();
            _suspendedGameRenderers.Clear();
            CaptureAndDisable<Camera>(scene);
            CaptureAndDisable<CinemachineBrain>(scene);
            CaptureAndDisable<AudioListener>(scene);
            CaptureAndDisable<Light>(scene);
            CaptureAndDisable<Canvas>(scene);
            CaptureAndDisable<UIDocument>(scene);
            CaptureAndDisable<EventSystem>(scene);
            CaptureAndDisableRenderers(scene);
        }

        /// <summary>恢复进入 SupplyScene 前保存的 GameScene 组件状态。</summary>
        private void RestoreGameScene()
        {
            for (int index = 0; index < _suspendedGameComponents.Count; index++)
            {
                (Behaviour component, bool wasEnabled) = _suspendedGameComponents[index];
                if (component != null)
                {
                    component.enabled = wasEnabled;
                }
            }

            _suspendedGameComponents.Clear();

            for (int index = 0; index < _suspendedGameRenderers.Count; index++)
            {
                (Renderer component, bool wasEnabled) = _suspendedGameRenderers[index];
                if (component != null)
                {
                    component.enabled = wasEnabled;
                }
            }

            _suspendedGameRenderers.Clear();
        }

        /// <summary>记录并禁用指定类型的场景组件。</summary>
        private void CaptureAndDisable<T>(Scene scene) where T : Behaviour
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                T[] components = roots[rootIndex].GetComponentsInChildren<T>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    T component = components[componentIndex];
                    _suspendedGameComponents.Add((component, component.enabled));
                    component.enabled = false;
                }
            }
        }

        /// <summary>保存并禁用场景中的全部 Renderer。</summary>
        private void CaptureAndDisableRenderers(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Renderer[] renderers = roots[rootIndex].GetComponentsInChildren<Renderer>(true);
                for (int componentIndex = 0; componentIndex < renderers.Length; componentIndex++)
                {
                    Renderer renderer = renderers[componentIndex];
                    _suspendedGameRenderers.Add((renderer, renderer.enabled));
                    renderer.enabled = false;
                }
            }
        }

        /// <summary>关闭指定类型的场景组件；SupplyScene 卸载前不需要保存状态。</summary>
        private static void DisableSceneComponents<T>(Scene scene) where T : Behaviour
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                T[] components = roots[rootIndex].GetComponentsInChildren<T>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    components[componentIndex].enabled = false;
                }
            }
        }

        /// <summary>设置场景内所有 EventSystem 的启用状态。</summary>
        /// <param name="scene">要处理的场景。</param>
        /// <param name="enabled">是否启用 EventSystem。</param>
        private static void SetEventSystemsEnabled(Scene scene, bool enabled)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                EventSystem[] eventSystems = roots[rootIndex].GetComponentsInChildren<EventSystem>(true);
                for (int eventSystemIndex = 0; eventSystemIndex < eventSystems.Length; eventSystemIndex++)
                {
                    eventSystems[eventSystemIndex].enabled = enabled;
                }
            }
        }

    }
}
