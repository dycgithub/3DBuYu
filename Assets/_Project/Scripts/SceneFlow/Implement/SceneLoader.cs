using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace GameSystem
{
    /// <summary>
    /// 基地-战斗场景交替加载器。挂在 DDOL(ScopeContainer)下,永不销毁。
    /// 进战斗:加载 GameLoopScene(Additive)→ 卸载 GameUIScene;
    /// 回基地:加载并激活 GameUIScene → 卸载 GameLoopScene。
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [Header("Loading UI")]
        public GameObject loadingCanvas;
        public Slider progressBar;
        public TextMeshProUGUI loadingText;

        [Header("场景名称")]
        public string mainMenuScene = "UIScene";
        public string gameScene = "GameScene";

        [Header("启动行为")]
        public bool autoLoadOnStart = true;

        private string _currentGameScene;
        private CanvasGroup _loadingGroup;
        private bool _isTransitioning;

        /// <summary>
        /// 隐藏加载界面，等待场景加载请求。
        /// </summary>
        private void Awake()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SetLoadingVisible(false);
        }

        /// <summary>根对象销毁时解除场景加载事件订阅。</summary>
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        /// <summary>
        /// 按配置决定是否在启动时加载战斗场景。
        /// </summary>
        private void Start()
        {
            if (autoLoadOnStart)
            {
                LoadGameScene();
            }
        }

        /// <summary>
        /// 以 Additive 模式加载战斗场景，并卸载主菜单场景。
        /// </summary>
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
        /// 卸载当前战斗场景，并重新加载主菜单场景。
        /// </summary>
        public void ReturnToMainMenu()
        {
            if (_isTransitioning)
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

        /// <summary>
        /// 执行战斗场景的异步加载、激活和主菜单卸载流程。
        /// </summary>
        /// <param name="sceneName">要加载的战斗场景名称。</param>
        private IEnumerator LoadSceneAdditive(string sceneName)
        {
            SetLoadingVisible(true);

            var uiScene = SceneManager.GetSceneByName(mainMenuScene);
            SetEventSystemsEnabled(uiScene, false);

            // 1. 附加加载战斗场景
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

            // 2. 卸载基地场景(全局容器/SceneLoader/LoadingPanel 均为 DDOL,不受影响)
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
        }

        /// <summary>
        /// 执行战斗场景卸载和主菜单场景加载流程。
        /// </summary>
        private IEnumerator UnloadGameScene()
        {
            SetLoadingVisible(true);

            // 1. 先加载并激活基地场景,再卸载当前活动的战斗场景。
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

            // 2. 卸载战斗场景
            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(_currentGameScene);
            while (unloadOperation != null && !unloadOperation.isDone)
            {
                yield return null;
            }
            _currentGameScene = null;

            SetLoadingVisible(false);
            _isTransitioning = false;
        }

        /// <summary>在新场景的 Start 执行前切换 Active Scene，保证池实例归属正确。</summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                (scene.name != gameScene && scene.name != mainMenuScene))
            {
                return;
            }

            SceneManager.SetActiveScene(scene);
        }

        /// <summary>
        /// 设置加载界面的可见性和输入拦截状态。
        /// </summary>
        /// <param name="visible">是否显示加载界面。</param>
        private void SetLoadingVisible(bool visible)
        {
            if (loadingCanvas == null)
            {
                return;
            }

            if (_loadingGroup == null)
            {
                _loadingGroup = loadingCanvas.GetComponent<CanvasGroup>() ?? loadingCanvas.AddComponent<CanvasGroup>();
            }
            _loadingGroup.alpha = visible ? 1f : 0f;
            _loadingGroup.blocksRaycasts = visible;
            _loadingGroup.interactable = visible;
        }

        /// <summary>
        /// 将异步加载进度更新到进度条和文本。
        /// </summary>
        /// <param name="progress">Unity 异步加载进度。</param>
        private void UpdateLoadingBar(float progress)
        {
            float clamped = Mathf.Clamp01(progress / 0.9f);
            if (progressBar != null)
            {
                progressBar.value = clamped;
            }

            if (loadingText != null)
            {
                loadingText.text = $"加载中... {clamped * 100f:F0}%";
            }
        }

        /// <summary>
        /// 设置场景内所有 EventSystem 的启用状态。
        /// </summary>
        /// <param name="scene">要处理的场景。</param>
        /// <param name="enabled">是否启用 EventSystem。</param>
        private static void SetEventSystemsEnabled(Scene scene, bool enabled)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var eventSystems = roots[i].GetComponentsInChildren<EventSystem>(true);
                for (int j = 0; j < eventSystems.Length; j++)
                {
                    eventSystems[j].enabled = enabled;
                }
            }
        }
    }
}
