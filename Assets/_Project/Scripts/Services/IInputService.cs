using System;
using System.Collections.Generic;
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
        PortFire8
    }

    /// <summary>
    /// 同一输入动作中的绑定位置。
    /// </summary>
    public enum InputBindingSlot
    {
        Primary,
        Secondary
    }

    public interface IInputService
    {
        Vector2 Move { get; }
        bool PausePressedThisFrame { get; }
        bool IsPortFireHeld(int portIndex);
        bool IsKeyHeld(KeyCode key);
        int MaxPorts { get; }
        event Action PausePressed;

        void RebindPause(Action onStarted, Action onCompleted, Action onCanceled);
        string GetPauseBindingPath();

        IReadOnlyList<InputBindingAction> BindingActions { get; }
        string GetBindingDisplayString(InputBindingAction action, InputBindingSlot slot);
        void Rebind(
            InputBindingAction action,
            InputBindingSlot slot,
            Action onStarted,
            Action onCompleted,
            Action onCanceled);
        void CancelRebind();
    }
}
