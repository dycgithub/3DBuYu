using _Project.UI.Animations;
using Services;
using UnityEngine;
using VContainer;

namespace _Project.UI.Settings
{
    /// <summary>聚合 GameScene 模态面板状态，统一屏蔽或恢复玩家攻击输入。</summary>
    public sealed class PanelAttackInputGate : MonoBehaviour
    {
        [SerializeField] private UIPanelShowHide[] _panels;

        [Inject] private IInputService _inputService;

        /// <summary>订阅所有模态面板并同步初始攻击门控状态。</summary>
        private void OnEnable()
        {
            if (_panels != null)
            {
                for (int index = 0; index < _panels.Length; index++)
                {
                    if (_panels[index] != null)
                    {
                        _panels[index].VisibilityChanged += HandleVisibilityChanged;
                    }
                }
            }

            ApplyAttackInputState();
        }

        /// <summary>注入完成后再同步一次，以覆盖 OnEnable 早于依赖注入的情况。</summary>
        private void Start()
        {
            ApplyAttackInputState();
        }

        /// <summary>解除全部面板事件并释放本组件持有的攻击门控。</summary>
        private void OnDisable()
        {
            if (_panels != null)
            {
                for (int index = 0; index < _panels.Length; index++)
                {
                    if (_panels[index] != null)
                    {
                        _panels[index].VisibilityChanged -= HandleVisibilityChanged;
                    }
                }
            }

            SetAttackInputState(AttackInputState.Allowed);
        }

        /// <summary>任一面板状态改变后重新计算聚合门控。</summary>
        private void HandleVisibilityChanged(bool _)
        {
            ApplyAttackInputState();
        }

        /// <summary>仅当所有绑定面板都隐藏时恢复攻击输入。</summary>
        private void ApplyAttackInputState()
        {
            if (_panels == null)
            {
                SetAttackInputState(AttackInputState.Allowed);
                return;
            }

            for (int index = 0; index < _panels.Length; index++)
            {
                if (_panels[index] != null && _panels[index].IsVisible)
                {
                    SetAttackInputState(AttackInputState.Blocked);
                    return;
                }
            }

            SetAttackInputState(AttackInputState.Allowed);
        }

        /// <summary>更新游戏输入服务的攻击门控。</summary>
        /// <param name="state">允许或屏蔽攻击输入。</param>
        private void SetAttackInputState(AttackInputState state)
        {
            _inputService?.SetAttackInputState(state);
        }
    }
}
