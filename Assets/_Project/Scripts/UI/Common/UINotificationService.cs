using System;
using Services;

namespace _Project.UI.Common
{
    public class UINotificationService : IUINotificationService
    {
        public event Action<NotificationMessage> OnNotificationRequested;

        public void ShowNotification(string message, NotificationKind type = NotificationKind.Info)
        {
            OnNotificationRequested?.Invoke(new NotificationMessage(message, type));
        }
    }
}
