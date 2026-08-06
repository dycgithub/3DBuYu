using System;
using UnityEngine;

namespace Services
{
    public interface IInputService
    {
        Vector2 Move { get; }
        bool PausePressedThisFrame { get; }
        bool IsPortFireHeld(int portIndex);
        int MaxPorts { get; }
        event Action PausePressed;

        void RebindPause(Action onStarted, Action onCompleted, Action onCanceled);
        string GetPauseBindingPath();
    }
}
