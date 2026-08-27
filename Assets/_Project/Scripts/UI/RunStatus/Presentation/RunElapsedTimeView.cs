using GameSystem;
using TMPro;
using UnityEngine;
using VContainer;

namespace _Project.UI.RunStatus
{
    /// <summary>
    /// 显示本局已经运行的时间。
    /// 时间状态由 GameManager 持有，本 View 只订阅变化并更新文本，不参与计时或结算。
    /// </summary>
    public sealed class RunElapsedTimeView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private string _format = "{0:D2}:{1:D2}";

        [Inject] private GameManager _gameManager;

        private bool _subscribed;

        private void Awake()
        {
            if (_label == null)
                _label = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            if (_label == null)
            {
                Debug.LogWarning("[RunElapsedTimeView] 未绑定 TextMeshProUGUI。", this);
                return;
            }

            if (_gameManager == null)
            {
                Debug.LogWarning("[RunElapsedTimeView] 未注入 GameManager。", this);
                return;
            }

            _gameManager.OnRunTimeChanged += HandleRunTimeChanged;
            _subscribed = true;
            HandleRunTimeChanged(_gameManager.Session != null ? _gameManager.Session.ElapsedTime : 0f);
        }

        private void OnDestroy()
        {
            if (_subscribed && _gameManager != null)
                _gameManager.OnRunTimeChanged -= HandleRunTimeChanged;
        }

        private void HandleRunTimeChanged(float elapsedTime)
        {
            if (_label == null)
                return;

            if (float.IsNaN(elapsedTime) || float.IsInfinity(elapsedTime))
                elapsedTime = 0f;

            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedTime));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            _label.text = string.Format(_format, minutes, seconds);
        }
    }
}
