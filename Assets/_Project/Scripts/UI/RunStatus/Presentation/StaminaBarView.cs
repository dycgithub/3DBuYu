using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.RunStatus
{
    /// <summary>
    /// 将玩家耐力绑定到三层 UGUI Slider：底槽、当前值和延迟追随层。
    /// 三个 Slider 的尺寸、颜色和层级由 Unity Inspector 中的 UI 布置决定。
    /// </summary>
    public sealed class StaminaBarView : MonoBehaviour
    {
        [Header("三层 Slider")]
        [SerializeField] private Slider _baseLayer;
        [SerializeField] private Slider _delayedLayer;
        [SerializeField] private Slider _currentLayer;

        [Header("表现")]
        [Tooltip("延迟层每秒追随目标值的归一化速度。")]
        [SerializeField, Min(0f)] private float _delayedFollowSpeed = 3f;

        [Inject] private StaminaController _stamina;

        private float _targetNormalized;
        private bool _subscribed;
        private bool _initialized;

        private void Start()
        {
            if (!HasAllLayers())
            {
                Debug.LogWarning("[StaminaBarView] 底槽、延迟层和当前层都必须绑定 Slider。", this);
                return;
            }

            if (_stamina == null)
            {
                Debug.LogWarning("[StaminaBarView] 未注入 StaminaController。", this);
                return;
            }

            ConfigureLayer(_baseLayer);
            ConfigureLayer(_delayedLayer);
            ConfigureLayer(_currentLayer);

            _stamina.OnStaminaChanged += HandleStaminaChanged;
            _subscribed = true;
            _initialized = false;
            HandleStaminaChanged(_stamina.currentStamina, _stamina.maxStamina);
        }

        private void Update()
        {
            if (_delayedLayer == null)
                return;

            float speed = Mathf.Max(0f, _delayedFollowSpeed) * Time.deltaTime;
            _delayedLayer.value = Mathf.MoveTowards(_delayedLayer.value, _targetNormalized, speed);
        }

        private void OnDestroy()
        {
            if (_subscribed && _stamina != null)
                _stamina.OnStaminaChanged -= HandleStaminaChanged;
        }

        private void HandleStaminaChanged(float currentStamina, float maximumStamina)
        {
            float normalized = maximumStamina > 0f
                ? Mathf.Clamp01(currentStamina / maximumStamina)
                : 0f;

            _targetNormalized = normalized;
            _currentLayer.value = normalized;

            // 首次刷新时两层重合；之后延迟层才表现出追随效果。
            if (!_initialized)
            {
                _delayedLayer.value = normalized;
                _initialized = true;
            }

            _baseLayer.value = 1f;
        }

        private bool HasAllLayers()
        {
            return _baseLayer != null && _delayedLayer != null && _currentLayer != null;
        }

        private static void ConfigureLayer(Slider layer)
        {
            layer.minValue = 0f;
            layer.maxValue = 1f;
            layer.interactable = false;
        }
    }
}
