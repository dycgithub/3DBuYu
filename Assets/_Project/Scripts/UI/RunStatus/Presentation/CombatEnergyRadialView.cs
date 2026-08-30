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
    /// 将本局能量绑定到一个 UGUI 环形 Image。
    /// 环形 Image 的填充量由本 View 直接同步，避免依赖 Slider 的矩形 Fill 行为。
    /// </summary>
    public sealed class CombatEnergyRadialView : MonoBehaviour
    {
        [SerializeField] private Image _fillImage;

        [Inject] private IEnergyService _energy;
        [Inject] private GameManager _gameManager;

        private IDisposable _energySubscription;
        private bool _runStartedSubscribed;

        private void Awake()
        {
            if (_fillImage == null)
                _fillImage = GetComponent<Image>();
        }

        private void Start()
        {
            if (_fillImage == null)
            {
                Debug.LogWarning("[CombatEnergyRadialView] 未绑定 Image。", this);
                return;
            }

            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Radial360;

            if (_energy == null)
            {
                Debug.LogWarning("[CombatEnergyRadialView] 未注入 IEnergyService。", this);
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
            if (_fillImage == null || _energy == null)
                return;

            float maximumEnergy = NormalizeNonNegative(_energy.MaximumEnergy);
            if (maximumEnergy <= 0f)
            {
                _fillImage.fillAmount = 0f;
                return;
            }

            float normalizedEnergy = NormalizeNonNegative(currentEnergy) / maximumEnergy;
            _fillImage.fillAmount = Mathf.Clamp01(normalizedEnergy);
        }

        private static float NormalizeNonNegative(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            return Mathf.Max(0f, value);
        }
    }
}
