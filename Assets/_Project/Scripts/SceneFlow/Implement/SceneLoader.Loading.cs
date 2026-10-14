using UnityEngine;
using UnityEngine.Localization;

namespace GameSystem
{
    /// <summary>管理加载界面表现和当前语言进度，保留异步场景切换流程。</summary>
    public partial class SceneLoader
    {
        private CanvasGroup _loadingGroup;
        private float _loadingProgress;

        /// <summary>设置加载界面的可见性和输入拦截状态。</summary>
        /// <param name="visible">是否显示加载界面。</param>
        private void SetLoadingVisible(bool visible)
        {
            if (loadingCanvas == null)
            {
                return;
            }

            if (_loadingGroup == null)
            {
                _loadingGroup = loadingCanvas.GetComponent<CanvasGroup>() ??
                    loadingCanvas.AddComponent<CanvasGroup>();
            }

            _loadingGroup.alpha = visible ? 1f : 0f;
            _loadingGroup.blocksRaycasts = visible;
            _loadingGroup.interactable = visible;
        }

        /// <summary>把异步加载进度写入进度条和当前语言的百分比模板。</summary>
        /// <param name="progress">Unity 异步加载进度。</param>
        private void UpdateLoadingBar(float progress)
        {
            _loadingProgress = progress;
            float clamped = Mathf.Clamp01(progress / 0.9f);
            if (progressBar != null)
            {
                progressBar.value = clamped;
            }

            if (loadingText != null)
            {
                loadingText.text = L10n.Get("scene.loadingProgress", clamped * 100f);
            }
        }

        /// <summary>语言切换时显示同一加载进度，不重新加载场景。</summary>
        private void RefreshLoadingLanguage(Locale _)
        {
            UpdateLoadingBar(_loadingProgress);
        }
    }
}
