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
    /// 回基地:卸载 GameLoopScene → 加载 GameUIScene。
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

        private string currentGameScene;
        private CanvasGroup _loadingGroup;

        private void Awake()
        {
            SetLoadingVisible(false);
        }

        private void Start()
        {
            if (autoLoadOnStart)
                LoadGameScene();
        }

        public void LoadGameScene()
        {
            if (SceneManager.GetSceneByName(gameScene).isLoaded)
                return;

            StartCoroutine(LoadSceneAdditive(gameScene));
        }

        public void ReturnToMainMenu()
        {
            if (string.IsNullOrEmpty(currentGameScene))
            {
                if (!SceneManager.GetSceneByName(gameScene).isLoaded)
                    return;

                currentGameScene = gameScene;
            }

            StartCoroutine(UnloadGameScene());
        }

        private IEnumerator LoadSceneAdditive(string sceneName)
        {
            SetLoadingVisible(true);

            var uiScene = SceneManager.GetSceneByName(mainMenuScene);
            SetEventSystemsEnabled(uiScene, false);

            // 1. 附加加载战斗场景
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                UpdateLoadingBar(op.progress);
                yield return null;
            }
            UpdateLoadingBar(1f);

            op.allowSceneActivation = true;
            while (!op.isDone)
                yield return null;

            // 2. 卸载基地场景(全局容器/SceneLoader/LoadingPanel 均为 DDOL,不受影响)
            if (uiScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(uiScene);
                while (unload != null && !unload.isDone)
                    yield return null;
            }

            SetLoadingVisible(false);

            currentGameScene = sceneName;
        }

        private IEnumerator UnloadGameScene()
        {
            SetLoadingVisible(true);

            // 1. 卸载战斗场景
            var op = SceneManager.UnloadSceneAsync(currentGameScene);
            while (op != null && !op.isDone)
                yield return null;
            currentGameScene = null;

            // 2. 重新加载基地场景(Single 模式,此时无任何已加载场景)
            var load = SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Single);
            while (load != null && !load.isDone)
                yield return null;

            SetLoadingVisible(false);
        }

        private void SetLoadingVisible(bool visible)
        {
            if (loadingCanvas == null) return;
            if (_loadingGroup == null)
                _loadingGroup = loadingCanvas.GetComponent<CanvasGroup>() ?? loadingCanvas.AddComponent<CanvasGroup>();
            _loadingGroup.alpha = visible ? 1f : 0f;
            _loadingGroup.blocksRaycasts = visible;
            _loadingGroup.interactable = visible;
        }

        private void UpdateLoadingBar(float progress)
        {
            float clamped = Mathf.Clamp01(progress / 0.9f);
            if (progressBar != null) progressBar.value = clamped;
            if (loadingText != null) loadingText.text = $"加载中... {clamped * 100f:F0}%";
        }

        private static void SetEventSystemsEnabled(Scene scene, bool enabled)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var eventSystems = roots[i].GetComponentsInChildren<EventSystem>(true);
                for (int j = 0; j < eventSystems.Length; j++)
                    eventSystems[j].enabled = enabled;
            }
        }
    }
}
