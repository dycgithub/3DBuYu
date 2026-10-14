using System;
using UnityEngine;

namespace _Project.UI.Quit.View
{
    /// <summary>绑定场景中的退出按钮；发布版关闭游戏，编辑器中停止 Play Mode。</summary>
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    internal sealed class QuitButtonView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _button;
        private Action _quitAction = QuitApplication;

        /// <summary>组件启用时绑定按钮，退出动作通过私有接缝便于独立验证。</summary>
        private void OnEnable()
        {
            _button.onClick.AddListener(RequestQuit);
        }

        /// <summary>组件禁用或场景卸载时移除监听，避免重复请求退出。</summary>
        private void OnDisable()
        {
            _button.onClick.RemoveListener(RequestQuit);
        }

        /// <summary>将用户点击交给平台对应的退出动作。</summary>
        private void RequestQuit()
        {
            _quitAction();
        }

        /// <summary>Windows Player 正常退出；编辑器仅结束运行，不关闭 Unity。</summary>
        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
