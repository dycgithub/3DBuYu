using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Services
{
    /// <summary>
    /// 设置面板允许重新绑定的输入动作。
    /// </summary>
    public enum InputBindingAction
    {
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight,
        Pause,
        PortFire1,
        PortFire2,
        PortFire3,
        PortFire4,
        PortFire5,
        PortFire6,
        PortFire7,
        PortFire8,
        CameraZoomIn,
        CameraZoomOut,
        CameraOrbitLeft,
        CameraOrbitRight,
        CameraOrbitUp,
        CameraOrbitDown
    }

    /// <summary>
    /// 菜单入口使用的固定输入动作。
    /// </summary>
    public enum MenuInputAction
    {
        Settings,
        Supply,
        Backpack
    }

    /// <summary>当前是否允许玩家发射器响应攻击输入。</summary>
    public enum AttackInputState
    {
        /// <summary>可以响应攻击输入。</summary>
        Allowed,

        /// <summary>面板遮挡期间忽略攻击输入。</summary>
        Blocked
    }

    /// <summary>提供玩家移动、攻击和菜单输入，并统一管理攻击输入门控。</summary>
    public interface IInputService
    {
        Vector2 Move { get; }
        /// <summary>相机距离输入；负数靠近玩家，正数远离玩家。</summary>
        float CameraDistanceInput { get; }
        /// <summary>相机环绕输入；X 为左右，Y 为上下。</summary>
        Vector2 CameraOrbitInput { get; }
        bool PausePressedThisFrame { get; }
        /// <summary>鼠标左键的持续按住状态。</summary>
        ReadOnlyReactiveProperty<bool> MouseFireHeld { get; }
        Vector2 PointerPosition { get; }
        AttackInputState CurrentAttackInputState { get; }
        bool IsPortFireHeld(int portIndex);
        bool IsMenuActionHeld(MenuInputAction action);
        int MaxPorts { get; }
        event Action PausePressed;
        /// <summary>左键在攻击门控允许时按下的单次事件。</summary>
        event Action MouseFirePressed;

        /// <summary>更新攻击输入门控状态。</summary>
        /// <param name="state">攻击输入允许或屏蔽状态。</param>
        void SetAttackInputState(AttackInputState state);

        void RebindPause(Action onStarted, Action onCompleted, Action onCanceled);
        string GetPauseBindingPath();

        IReadOnlyList<InputBindingAction> BindingActions { get; }
        string GetBindingDisplayString(InputBindingAction action);
        void Rebind(
            InputBindingAction action,
            Action onStarted,
            Action onCompleted,
            Action onCanceled);
        void CancelRebind();
        /// <summary>清除所有可重绑动作的覆盖并恢复输入资产默认键位。</summary>
        void ResetBindings();
    }
}
