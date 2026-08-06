using System;
using Services;

namespace _Project.UI.Common
{
    public class UINotificationService : IUINotificationService
    {
        public event Action<NotificationMessage> OnToastRequested;

        public void ShowToast(string message, NotificationKind type = NotificationKind.Info)
        {
            OnToastRequested?.Invoke(new NotificationMessage(message, type));
        }
    }
}
