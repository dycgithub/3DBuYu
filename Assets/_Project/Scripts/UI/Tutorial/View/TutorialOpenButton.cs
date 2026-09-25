using UnityEngine;
using UnityEngine.UI;
using VContainer;
using _Project.UI.Tutorial.Implement;
using _Project.UI.Tutorial.Model;

namespace _Project.UI.Tutorial.View
{
    /// <summary>将一个 UGUI 按钮绑定到指定教程配置。</summary>
    [RequireComponent(typeof(Button))]
    public sealed class TutorialOpenButton : MonoBehaviour
    {
        [SerializeField] private TutorialSequenceSO _sequence;

        [Inject] private TutorialPanelController _controller;
        private Button _button;

        /// <summary>缓存按钮并绑定点击事件。</summary>
        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OpenTutorial);
        }

        /// <summary>移除按钮监听。</summary>
        private void OnDestroy()
        {
            _button?.onClick.RemoveListener(OpenTutorial);
        }

        /// <summary>请求控制器打开配置的教程。</summary>
        private void OpenTutorial()
        {
            _controller?.Open(_sequence);
        }
    }
}
