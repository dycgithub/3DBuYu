using System.Collections;
using System.Collections.Generic;
using Services;
using TMPro;
using UnityEngine;
using Utils;
using VContainer;

namespace _Project.UI.Common
{
    public class UINotificationView : MonoBehaviour
    {
        private sealed class NotificationPresentation
        {
            public TextMeshProUGUI Text;
            public CanvasGroup CanvasGroup;
        }

        [SerializeField] private GameObject _notificationPrefab;
        [SerializeField] private Transform _notificationContainer;
        [SerializeField, Min(0f)] private float _displayDuration = 2f;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.3f;
        [SerializeField, Min(1)] private int _maxVisible = 3;
        [SerializeField, Min(1)] private int _maxPending = 20;
        [SerializeField, Min(0)] private int _prewarmCount;
        [SerializeField, Min(1)] private int _maximumRetained = 12;

        private readonly Queue<NotificationMessage> _pending = new();
        private readonly Dictionary<int, NotificationPresentation> _presentations = new();
        private readonly HashSet<GameObject> _activeNotifications = new();
        private int _activeCount;

        [Inject] private IUINotificationService _notificationService;
        [Inject] private IGameObjectPool _pool;

        private void Start()
        {
            if (_notificationService != null)
                _notificationService.OnNotificationRequested += EnqueueNotification;

            if (_notificationPrefab != null && _pool != null && _prewarmCount > 0)
            {
                PoolSettings settings = GetPoolSettings();
                _pool.Prewarm(_notificationPrefab, settings, _prewarmCount);
            }
        }

        private void OnDestroy()
        {
            if (_notificationService != null)
                _notificationService.OnNotificationRequested -= EnqueueNotification;

            foreach (GameObject notification in _activeNotifications)
                _pool?.Return(notification);

            _activeNotifications.Clear();
            _presentations.Clear();
        }

        private void EnqueueNotification(NotificationMessage message)
        {
            if (_notificationPrefab == null || _pool == null)
                return;

            if (_pending.Count >= _maxPending)
                _pending.Dequeue();

            _pending.Enqueue(message);
            if (_activeCount < _maxVisible)
                ShowNext();
        }

        private void ShowNext()
        {
            if (_pending.Count == 0)
                return;

            NotificationMessage message = _pending.Dequeue();
            _activeCount++;
            StartCoroutine(ShowNotificationRoutine(message));
        }

        private IEnumerator ShowNotificationRoutine(NotificationMessage message)
        {
            GameObject notification = _pool.Rent(
                _notificationPrefab,
                GetPoolSettings(),
                _notificationContainer ?? transform);
            if (notification == null)
            {
                FinishNotification(null);
                yield break;
            }

            _activeNotifications.Add(notification);
            notification.transform.SetAsLastSibling();

            NotificationPresentation presentation = GetPresentation(notification);
            if (presentation.Text != null)
                presentation.Text.text = message.message;

            presentation.CanvasGroup.alpha = 0f;
            yield return Fade(presentation.CanvasGroup, 0f, 1f, _fadeDuration);
            yield return WaitUnscaled(_displayDuration);
            yield return Fade(presentation.CanvasGroup, 1f, 0f, _fadeDuration);

            FinishNotification(notification);
        }

        private NotificationPresentation GetPresentation(GameObject notification)
        {
            int instanceId = notification.GetInstanceID();
            if (_presentations.TryGetValue(instanceId, out NotificationPresentation presentation))
                return presentation;

            CanvasGroup canvasGroup = notification.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = notification.AddComponent<CanvasGroup>();

            presentation = new NotificationPresentation
            {
                Text = notification.GetComponentInChildren<TextMeshProUGUI>(true),
                CanvasGroup = canvasGroup
            };
            _presentations.Add(instanceId, presentation);
            return presentation;
        }

        private IEnumerator Fade(CanvasGroup canvasGroup, float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void FinishNotification(GameObject notification)
        {
            if (notification != null)
            {
                _activeNotifications.Remove(notification);
                _pool.Return(notification);
            }

            _activeCount = Mathf.Max(0, _activeCount - 1);
            ShowNext();
        }

        private PoolSettings GetPoolSettings()
        {
            return new PoolSettings(_prewarmCount, _maximumRetained);
        }
    }
}
