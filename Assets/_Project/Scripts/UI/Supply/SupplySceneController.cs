using Cinemachine;
using GameSystem;
using Supply;
using UnityEngine;
using VContainer;

namespace _Project.UI.Supply
{
    /// <summary>
    /// 管理 SupplyScene 的入口 Virtual Camera 和返回 GameScene 按钮。
    /// 不负责抽奖服务、奖励生成或积分结算。
    /// </summary>
    public sealed class SupplySceneController : MonoBehaviour
    {
        private const int ActiveCameraPriority = 20;
        private const int InactiveCameraPriority = 0;

        [Header("入口摄像机")]
        [SerializeField] private CinemachineVirtualCamera _chestCamera;
        [SerializeField] private CinemachineVirtualCamera _slotMachineCamera;
        [SerializeField] private CinemachineVirtualCamera _mobiusCamera;

        [Header("场景导航")]
        [SerializeField] private UnityEngine.UI.Button _returnButton;

        [Inject] private SceneLoader _sceneLoader;

        private bool _isBound;

        /// <summary>绑定返回按钮，并按入口参数选择摄像机。</summary>
        private void Start()
        {
            _returnButton ??= FindButton("SupplyReturnButton");
            _returnButton?.onClick.AddListener(ReturnToGame);
            _isBound = true;

            SupplyMethod method = _sceneLoader != null
                ? _sceneLoader.ConsumeSupplyMethod()
                : SupplyMethod.Chest;
            SelectCamera(method);
        }

        /// <summary>解除返回按钮监听。</summary>
        private void OnDestroy()
        {
            if (!_isBound)
            {
                return;
            }

            _returnButton?.onClick.RemoveListener(ReturnToGame);
        }

        /// <summary>将三个入口 Virtual Camera 设置为互斥优先级。</summary>
        /// <param name="method">当前入口对应的抽奖方法。</param>
        private void SelectCamera(SupplyMethod method)
        {
            SetPriority(_chestCamera, InactiveCameraPriority);
            SetPriority(_slotMachineCamera, InactiveCameraPriority);
            SetPriority(_mobiusCamera, InactiveCameraPriority);

            CinemachineVirtualCamera activeCamera = method switch
            {
                SupplyMethod.Chest => _chestCamera,
                SupplyMethod.SlotMachine => _slotMachineCamera,
                SupplyMethod.Mobius => _mobiusCamera,
                _ => _chestCamera
            };

            if (activeCamera == null)
            {
                Debug.LogError($"[SupplySceneController] 未配置入口摄像机: {method}。", this);
                return;
            }

            SetPriority(activeCamera, ActiveCameraPriority);
        }

        /// <summary>执行返回 GameScene 的异步切换请求。</summary>
        private void ReturnToGame()
        {
            if (_sceneLoader == null)
            {
                Debug.LogError("[SupplySceneController] 未注入 SceneLoader，无法返回 GameScene。", this);
                return;
            }

            _returnButton.interactable = false;
            if (!_sceneLoader.TryReturnToGameScene())
            {
                _returnButton.interactable = true;
            }
        }

        /// <summary>设置摄像机优先级；空引用只跳过该摄像机。</summary>
        /// <param name="camera">待设置的 Virtual Camera。</param>
        /// <param name="priority">Cinemachine 优先级。</param>
        private static void SetPriority(CinemachineVirtualCamera camera, int priority)
        {
            if (camera != null)
            {
                camera.Priority = priority;
            }
        }

        /// <summary>按对象名查找 SupplyScene 中的返回按钮。</summary>
        /// <param name="objectName">按钮对象名称。</param>
        /// <returns>找到的按钮；不存在时返回 null。</returns>
        private UnityEngine.UI.Button FindButton(string objectName)
        {
            UnityEngine.UI.Button[] buttons =
                GetComponentsInChildren<UnityEngine.UI.Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                if (buttons[index].name == objectName)
                {
                    return buttons[index];
                }
            }

            return null;
        }
    }
}
