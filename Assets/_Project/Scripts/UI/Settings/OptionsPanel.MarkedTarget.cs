using CombatSystem;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI.Settings
{
    /// <summary>绑定设置面板中的标记攻击模式 Toggle。</summary>
    public partial class OptionsPanel
    {
        [SerializeField] private Toggle _attackModeMarkedTargetToggle;

        /// <summary>选择点击锁定并按队列顺序集火的模式。</summary>
        private void OnMarkedTargetAttackChanged(bool selected)
        {
            if (selected)
            {
                optionsService?.SetAttackMode(TransmitterAttackMode.MarkedTarget);
            }
        }
    }
}
