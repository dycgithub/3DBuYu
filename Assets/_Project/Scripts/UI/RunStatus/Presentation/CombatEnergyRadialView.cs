using System;
using GameSystem;
using R3;
using Services;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.RunStatus
{
    /// <summary>
    /// 将本局能量绑定到一个 UGUI Slider。
    /// 环形外观由 Slider 的 Fill Image 在 Unity Inspector 中配置，本 View 只负责数值同步。
    /// </summary>
    public sealed class CombatEnergyRadialView : MonoBehaviour
    {
        [SerializeField] private Slider _slider;

        [Inject] private ICombatEnergyService _energy;
        [Inject] private GameManager _gameManager;

        private IDisposable _energySubscription;
        private bool _runStartedSubscribed;

        private void Awake()
        {
            if (_slider == null)
                _slider = GetComponent<Slider>();
        }

        private void Start()
        {
            if (_slider == null)
            {
                Debug.LogWarning("[CombatEnergyRadialView] 未绑定 Slider。", this);
                return;
            }

            _slider.interactable = false;

            if (_energy == null)
            {
                Debug.LogWarning("[CombatEnergyRadialView] 未注入 ICombatEnergyService。", this);
                return;
            }

            _energySubscription = _energy.CurrentEnergy.Subscribe(HandleEnergyChanged);

            if (_gameManager != null)
            {
                _gameManager.OnGameStarted += Refresh;
                _runStartedSubscribed = true;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            _energySubscription?.Dispose();

            if (_runStartedSubscribed && _gameManager != null)
                _gameManager.OnGameStarted -= Refresh;
        }

        private void HandleEnergyChanged(float currentEnergy)
        {
            Refresh(currentEnergy);
        }

        private void Refresh()
        {
            if (_energy != null)
                Refresh(_energy.CurrentEnergy.CurrentValue);
        }

        private void Refresh(float currentEnergy)
        {
            if (_slider == null || _energy == null)
                return;

            float maximumEnergy = NormalizeNonNegative(_energy.MaximumEnergy);
            _slider.minValue = 0f;
            _slider.maxValue = maximumEnergy;
            _slider.value = Mathf.Clamp(NormalizeNonNegative(currentEnergy), 0f, maximumEnergy);
        }

        private static float NormalizeNonNegative(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            return Mathf.Max(0f, value);
        }
    }
}
