using System.Collections;
using System.Collections.Generic;
using Services;
using TMPro;
using UnityEngine;
using VContainer;

namespace _Project.UI.Common
{
    public class UINotificationView : MonoBehaviour
    {
        [SerializeField] private GameObject _toastPrefab;
        [SerializeField] private Transform _toastContainer;
        [SerializeField] private float _displayDuration = 2f;
        [SerializeField] private float _fadeDuration = 0.3f;
        [SerializeField] private int _maxVisible = 3;

        private readonly Queue<NotificationMessage> _pending = new();
        private int _activeCount;

        [Inject] private IUINotificationService _toastService;

        private void Start()
        {
            if (_toastService != null)
                _toastService.OnToastRequested += EnqueueToast;
        }

        private void OnDestroy()
        {
            if (_toastService != null)
                _toastService.OnToastRequested -= EnqueueToast;
        }

        private void EnqueueToast(NotificationMessage msg)
        {
            _pending.Enqueue(msg);
            if (_activeCount < _maxVisible)
                ShowNext();
        }

        private void ShowNext()
        {
            if (_pending.Count == 0) return;
            var msg = _pending.Dequeue();
            _activeCount++;
            StartCoroutine(ShowToastRoutine(msg));
        }

        private IEnumerator ShowToastRoutine(NotificationMessage msg)
        {
            var go = Instantiate(_toastPrefab, _toastContainer ?? transform);
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = msg.message;

            var canvasGroup = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;

            yield return new WaitForSeconds(_displayDuration);

            elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(1f - elapsed / _fadeDuration);
                yield return null;
            }

            Destroy(go);
            _activeCount--;
            ShowNext();
        }
    }
}
