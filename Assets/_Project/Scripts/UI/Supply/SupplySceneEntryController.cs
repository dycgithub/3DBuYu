using GameSystem;
using Supply;
using TMPro;
using UnityEngine;
using VContainer;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace _Project.UI.Supply
{
    /// <summary>
    /// 管理 GameScene 中进入 SupplyScene 的三个入口按钮。
    /// <para>
    /// 负责入场请求、当前语言费用显示、战斗暂停和返回恢复，不执行抽奖逻辑。
    /// </para>
    /// </summary>
    public sealed class SupplySceneEntryController : MonoBehaviour
    {
        [Header("抽奖入口")]
        [SerializeField] private UnityEngine.UI.Button _chestButton;
        [SerializeField] private UnityEngine.UI.Button _slotMachineButton;
        [SerializeField] private UnityEngine.UI.Button _mobiusButton;

        [Inject] private SceneLoader _sceneLoader;
        [Inject] private GameManager _gameManager;
        [Inject] private SupplyEntryService _entryService;

        private bool _isBound;
        private bool _pausedForSupply;

        /// <summary>提前解析场景中预置的入口按钮。</summary>
        private void Awake()
        {
            _chestButton ??= FindButton("SupplyChestButton");
            _slotMachineButton ??= FindButton("SupplySlotMachineButton");
            _mobiusButton ??= FindButton("SupplyMobiusButton");
        }

        /// <summary>绑定按钮和场景切换完成事件。</summary>
        private void Start()
        {
            _chestButton?.onClick.AddListener(OpenChest);
            _slotMachineButton?.onClick.AddListener(OpenSlotMachine);
            _mobiusButton?.onClick.AddListener(OpenMobius);
            SetEntryLabels();
            LocalizationSettings.SelectedLocaleChanged += RefreshLanguage;

            if (_sceneLoader != null)
            {
                _sceneLoader.SceneChanged += HandleSceneChanged;
                _sceneLoader.SupplySceneLoaded += HandleSupplySceneLoaded;
                _sceneLoader.SupplySceneLoadFailed += HandleSupplySceneLoadFailed;
            }

            _isBound = true;
        }

        /// <summary>解除按钮和场景事件监听，避免场景销毁后继续回调。</summary>
        private void OnDestroy()
        {
            if (!_isBound)
            {
                return;
            }

            _chestButton?.onClick.RemoveListener(OpenChest);
            _slotMachineButton?.onClick.RemoveListener(OpenSlotMachine);
            _mobiusButton?.onClick.RemoveListener(OpenMobius);
            LocalizationSettings.SelectedLocaleChanged -= RefreshLanguage;
            if (_sceneLoader != null)
            {
                _sceneLoader.SceneChanged -= HandleSceneChanged;
                _sceneLoader.SupplySceneLoaded -= HandleSupplySceneLoaded;
                _sceneLoader.SupplySceneLoadFailed -= HandleSupplySceneLoadFailed;
            }
        }

        /// <summary>进入宝箱开箱入口。</summary>
        private void OpenChest()
        {
            Open(SupplyMethod.Chest);
        }

        /// <summary>进入老虎机入口。</summary>
        private void OpenSlotMachine()
        {
            Open(SupplyMethod.SlotMachine);
        }

        /// <summary>进入莫比乌斯奖池入口。</summary>
        private void OpenMobius()
        {
            Open(SupplyMethod.Mobius);
        }

        /// <summary>
        /// 请求加载 SupplyScene，并在当前战斗正在运行时暂停它。
        /// </summary>
        /// <param name="method">待进入的 SupplyScene 入口。</param>
        private void Open(SupplyMethod method)
        {
            if (_sceneLoader == null || _gameManager == null || _entryService == null)
            {
                Debug.LogError("[SupplySceneEntryController] 缺少入口服务或场景依赖。", this);
                return;
            }

            if (!_entryService.TryBeginEntry(method))
            {
                return;
            }

            if (!_sceneLoader.TryLoadSupplyScene(method))
            {
                _entryService.CancelEntry(method);
                return;
            }

            DisableButtons();
            if (_gameManager.CurrentState == GameState.Playing)
            {
                _gameManager.TogglePause();
                _pausedForSupply = true;
            }
        }

        /// <summary>
        /// 在 SupplyScene 返回 GameScene 后恢复本次进入前的战斗输入。
        /// </summary>
        /// <param name="sceneName">完成切换后的场景名称。</param>
        private void HandleSceneChanged(string sceneName)
        {
            if (_sceneLoader == null || sceneName != _sceneLoader.gameScene)
            {
                return;
            }

            ResumeBattle();
            EnableButtons();
        }

        /// <summary>确认成功进入后保留已扣除的入场费。</summary>
        /// <param name="method">成功加载的抽奖入口。</param>
        private void HandleSupplySceneLoaded(SupplyMethod method)
        {
            _entryService?.ConfirmEntry(method);
        }

        /// <summary>加载失败时退款并恢复战斗入口状态。</summary>
        /// <param name="method">加载失败的抽奖入口。</param>
        private void HandleSupplySceneLoadFailed(SupplyMethod method)
        {
            _entryService?.CancelEntry(method);
            ResumeBattle();
            EnableButtons();
        }

        /// <summary>仅恢复由本次 Supply 入口暂停的战斗。</summary>
        private void ResumeBattle()
        {
            if (_pausedForSupply && _gameManager != null &&
                _gameManager.CurrentState == GameState.Paused)
            {
                _gameManager.TogglePause();
            }

            _pausedForSupply = false;
        }

        /// <summary>从翻译表重建三个入口的名称和费用，重复刷新不会叠加文本。</summary>
        private void SetEntryLabels()
        {
            if (_entryService == null)
            {
                return;
            }

            SetEntryLabel(_chestButton, SupplyMethod.Chest);
            SetEntryLabel(_slotMachineButton, SupplyMethod.SlotMachine);
            SetEntryLabel(_mobiusButton, SupplyMethod.Mobius);
        }

        /// <summary>在按钮已有 TMP 文本中显示对应入场费。</summary>
        /// <param name="button">抽奖入口按钮。</param>
        /// <param name="method">按钮对应的抽奖入口。</param>
        private void SetEntryLabel(UnityEngine.UI.Button button, SupplyMethod method)
        {
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null)
            {
                string key = method == SupplyMethod.Chest ? "ui.chest" :
                    method == SupplyMethod.SlotMachine ? "ui.slotMachine" : "ui.mobius";
                label.text = L10n.Get("supply.entry", L10n.Get(key), _entryService.GetCost(method));
            }
        }

        /// <summary>语言变化时刷新已配置的入场文本。</summary>
        private void RefreshLanguage(Locale _)
        {
            SetEntryLabels();
        }

        /// <summary>禁用入口按钮，避免异步切换期间重复提交请求。</summary>
        private void DisableButtons()
        {
            if (_chestButton != null)
            {
                _chestButton.interactable = false;
            }

            if (_slotMachineButton != null)
            {
                _slotMachineButton.interactable = false;
            }

            if (_mobiusButton != null)
            {
                _mobiusButton.interactable = false;
            }
        }

        /// <summary>恢复入口按钮交互。</summary>
        private void EnableButtons()
        {
            if (_chestButton != null)
            {
                _chestButton.interactable = true;
            }

            if (_slotMachineButton != null)
            {
                _slotMachineButton.interactable = true;
            }

            if (_mobiusButton != null)
            {
                _mobiusButton.interactable = true;
            }
        }

        /// <summary>按对象名查找当前入口面板下的 UGUI 按钮。</summary>
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
