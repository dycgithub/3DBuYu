using System;

namespace Services
{
    public enum NotificationKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    public interface IUINotificationService
    {
        void ShowToast(string message, NotificationKind type = NotificationKind.Info);
        event Action<NotificationMessage> OnToastRequested;
    }

    public struct NotificationMessage
    {
        public string message;
        public NotificationKind type;

        public NotificationMessage(string msg, NotificationKind t)
        {
            message = msg;
            type = t;
        }
    }
}
