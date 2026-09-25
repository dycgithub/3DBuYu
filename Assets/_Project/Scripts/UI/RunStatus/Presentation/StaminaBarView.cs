using System;
using R3;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.RunStatus
{
    /// <summary>
    /// 将玩家耐力显示为背景轨道、连续当前填充和逐段追随填充。
    /// 当前填充保留精确值，追随填充按 20 段逐段追赶耐力下降后的目标值。
    /// </summary>
    public sealed class StaminaBarView : MonoBehaviour
    {
        private const int DisplaySegmentCount = 20;
        private const float SegmentInterval = 1f / DisplaySegmentCount;

        [Header("耐力条视觉层")]
        [FormerlySerializedAs("_baseLayer")]
        [Tooltip("始终显示完整耐力范围的背景轨道。")]
        [SerializeField] private Slider _backgroundTrack;
        [FormerlySerializedAs("_delayedLayer")]
        [Tooltip("按分段逐步追赶当前填充的填充层。")]
        [SerializeField] private Slider _trailingFill;
        [FormerlySerializedAs("_currentLayer")]
        [Tooltip("显示精确当前耐力值的填充层。")]
        [SerializeField] private Slider _currentFill;

        [Header("表现")]
        [FormerlySerializedAs("_delayedFollowSpeed")]
        [Tooltip("耐力下降时，追随填充每秒跨越的分段数量。")]
        [SerializeField, Min(0f)] private float _trailingFillSpeed = 4f;

        [Inject] private StaminaController _stamina;

        private int _targetSegment;
        private int _displayedSegment;
        private float _segmentTimer;
        private float _lastNormalizedValue;
        private bool _hasCurrentValue;
        private bool _isReady;
        private IDisposable _staminaSubscription;

        private void Start()
        {
            if (!HasRequiredLayers())
            {
                Debug.LogWarning(
                    "[StaminaBarView] 背景轨道、追随填充和当前填充都必须绑定 Slider。",
                    this);

                enabled = false;
                return;
            }

            if (_stamina == null)
            {
                Debug.LogWarning(
                    "[StaminaBarView] 未注入 StaminaController。",
                    this);

                enabled = false;
                return;
            }

            ConfigureLayer(_backgroundTrack);
            ConfigureLayer(_trailingFill);
            ConfigureLayer(_currentFill);
            _backgroundTrack.SetValueWithoutNotify(1f);
            _isReady = true;

            _staminaSubscription = _stamina.CurrentStamina.Subscribe(
                HandleStaminaChanged);
        }

        private void Update()
        {
            if (!_isReady || _displayedSegment == _targetSegment)
            {
                return;
            }

            AdvanceFollow(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _staminaSubscription?.Dispose();
            _staminaSubscription = null;
        }

        private void HandleStaminaChanged(float currentStamina)
        {
            float normalizedValue = NormalizeValue(
                currentStamina,
                _stamina.maxStamina);
            int targetSegment = GetTargetSegment(normalizedValue);

            _currentFill.SetValueWithoutNotify(normalizedValue);

            if (!_hasCurrentValue)
            {
                _hasCurrentValue = true;
                _lastNormalizedValue = normalizedValue;
                _targetSegment = targetSegment;
                _displayedSegment = targetSegment;
                _segmentTimer = 0f;
                ApplyTrailingValue();
                return;
            }

            bool isRecovering = normalizedValue > _lastNormalizedValue;
            _lastNormalizedValue = normalizedValue;

            if (isRecovering)
            {
                _targetSegment = targetSegment;
                _displayedSegment = targetSegment;
                _segmentTimer = 0f;
                ApplyTrailingValue();
                return;
            }

            if (targetSegment != _targetSegment)
            {
                _targetSegment = targetSegment;
                _segmentTimer = 0f;
            }
        }

        private void AdvanceFollow(float deltaTime)
        {
            if (_displayedSegment == _targetSegment)
            {
                _segmentTimer = 0f;
                return;
            }

            if (float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime) ||
                deltaTime <= 0f)
            {
                return;
            }

            float speed = NormalizeSpeed(_trailingFillSpeed);
            if (speed <= 0f)
            {
                return;
            }

            _segmentTimer += deltaTime;
            float segmentDuration = 1f / speed;
            while (_segmentTimer >= segmentDuration &&
                   _displayedSegment != _targetSegment)
            {
                _segmentTimer -= segmentDuration;
                int direction = _displayedSegment < _targetSegment ? 1 : -1;
                _displayedSegment += direction;
                ApplyTrailingValue();
            }

            if (_displayedSegment == _targetSegment)
            {
                _segmentTimer = 0f;
            }
        }

        private void ApplyTrailingValue()
        {
            _trailingFill.SetValueWithoutNotify(
                _displayedSegment * SegmentInterval);
        }

        private static float NormalizeValue(
            float currentStamina,
            float maximumStamina)
        {
            if (float.IsNaN(currentStamina) ||
                float.IsInfinity(currentStamina) ||
                float.IsNaN(maximumStamina) ||
                float.IsInfinity(maximumStamina) ||
                maximumStamina <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(currentStamina / maximumStamina);
        }

        private static float NormalizeSpeed(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                return 0f;
            }

            return Mathf.Max(0f, speed);
        }

        private static int GetTargetSegment(float normalizedValue)
        {
            if (normalizedValue <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(
                Mathf.CeilToInt(normalizedValue * DisplaySegmentCount),
                0,
                DisplaySegmentCount);
        }

        private bool HasRequiredLayers()
        {
            return _backgroundTrack != null &&
                   _trailingFill != null &&
                   _currentFill != null;
        }

        private static void ConfigureLayer(Slider layer)
        {
            layer.minValue = 0f;
            layer.maxValue = 1f;
            layer.interactable = false;
        }
    }
}
