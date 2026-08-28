using System;
using System.Collections.Generic;
using R3;
using Services;
using TMPro;
using UnityEngine;
using VContainer;
using _Project.UI.Animations;

namespace _Project.UI.MetaballMenu
{
    /// <summary>
    /// 管理居中融球菜单的轨道、按键、能量支付和面板入口。
    /// 表现层只负责显示，能量仍由战斗 Scope 中的 ICombatEnergyService 所有。
    /// </summary>
    public sealed class MetaballMenuController : MonoBehaviour
    {
        [Header("表现")]
        [SerializeField] private MetaballFieldGraphic _field;
        [SerializeField] private TMP_Text _energyLabel;
        [SerializeField] private List<MetaballMenuItem> _items = new();
        [SerializeField] private float _orbitRadius = 230f;
        [SerializeField] private float _orbitSpeed = 24f;
        [SerializeField] private float _centerRadius = 104f;
        [SerializeField] private Color _centerColor = new(0.2f, 0.85f, 1f, 1f);

        [Header("目标面板")]
        [SerializeField] private UIPanelShowHide _settingsPanel;
        [SerializeField] private UIPanelShowHide _supplyPanel;
        [SerializeField] private UIPanelShowHide _backpackPanel;

        [Inject] private IInputService _input;
        [Inject] private IEnergyService _energy;
        [Inject] private ICombatPhaseService _combatPhase;

        private IDisposable _energySubscription;
        private float _orbitAngle;
        private bool _wasPanelVisible;

        private void Awake()
        {
            if (_field == null)
                _field = GetComponentInChildren<MetaballFieldGraphic>(true);

            if (_items == null)
                _items = new List<MetaballMenuItem>();
            else if (_items.Count == 0)
                _items.AddRange(GetComponentsInChildren<MetaballMenuItem>(true));
        }

        private void Start()
        {
            if (_energy != null)
                _energySubscription = _energy.CurrentEnergy.Subscribe(UpdateEnergyLabel);
            else
                Debug.LogWarning("[MetaballMenuController] 未注入 ICombatEnergyService，融球不会推进。", this);

            if (_input == null)
                Debug.LogWarning("[MetaballMenuController] 未注入 IInputService，融球不会响应按键。", this);
            if (_combatPhase == null)
                Debug.LogWarning("[MetaballMenuController] 未注入 ICombatPhaseService，融球不会推进。", this);
            if (_field == null)
                Debug.LogWarning("[MetaballMenuController] 未绑定 MetaballFieldGraphic。", this);

            for (int i = 0; i < _items.Count; i++)
                _items[i]?.InitializeRuntime();
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return;

            bool panelVisible = IsAnyTargetPanelVisible();
            if (panelVisible != _wasPanelVisible)
            {
                ResetAllAndRequireRelease();
                _wasPanelVisible = panelVisible;
            }

            _orbitAngle = Mathf.Repeat(_orbitAngle + _orbitSpeed * deltaTime, 360f);
            _field?.ClearBalls();
            _field?.SetBall(0, Vector2.zero, Mathf.Max(1f, _centerRadius), _centerColor);

            bool canFuse = !panelVisible && _combatPhase != null && _combatPhase.CanPerformCombatActions;
            MetaballMenuItem completedItem = null;

            for (int i = 0; i < _items.Count; i++)
            {
                MetaballMenuItem item = _items[i];
                if (item == null)
                    continue;

                item.InitializeRuntime();
                MetaballFusionProgress progress = item.Progress;
                bool isHeld = _input != null && item.HoldKey != KeyCode.None && _input.IsKeyHeld(item.HoldKey);
                bool paymentSucceeded = false;

                if (canFuse && isHeld && !progress.IsComplete && !progress.RequiresRelease)
                    paymentSucceeded = TryPayEnergy(item, deltaTime);

                bool justCompleted = progress.Advance(deltaTime, isHeld, paymentSucceeded);
                float orbitRadius = item.OrbitRadius > 0f ? item.OrbitRadius : Mathf.Max(0f, _orbitRadius);
                float angle = (item.OrbitAngle + _orbitAngle) * Mathf.Deg2Rad;
                Vector2 orbitPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;
                Vector2 position = Vector2.Lerp(orbitPosition, Vector2.zero, Mathf.SmoothStep(0f, 1f, progress.Value));

                item.SetLocalPosition(position);
                item.RefreshView(progress.Value, isHeld, !canFuse || progress.RequiresRelease);
                _field?.SetBall(i + 1, position, item.BallRadius, item.BallColor);

                if (justCompleted)
                {
                    completedItem = item;
                    break;
                }
            }

            if (completedItem != null)
                OpenTarget(completedItem.Target);
        }

        private bool TryPayEnergy(MetaballMenuItem item, float deltaTime)
        {
            if (_energy == null)
                return false;

            float cost = item.EnergyCostPerSecond * deltaTime;
            if (cost <= 0f)
                return true;

            return _energy.TrySpend(cost, EnergySpendKind.MenuFusion) && !_energy.IsDepleted;
        }

        private void OpenTarget(MetaballMenuTarget target)
        {
            UIPanelShowHide panel = target switch
            {
                MetaballMenuTarget.Settings => _settingsPanel,
                MetaballMenuTarget.Supply => _supplyPanel,
                MetaballMenuTarget.Backpack => _backpackPanel,
                _ => null
            };

            if (panel == null)
            {
                Debug.LogWarning($"[MetaballMenuController] 目标面板未配置: {target}", this);
                return;
            }

            // 补给面板在当前场景中默认 inactive，必须先激活才能执行 UIPanelShowHide.Awake 初始化。
            if (!panel.gameObject.activeSelf)
                panel.gameObject.SetActive(true);

            panel.Show();
            ResetAllAndRequireRelease();
            _wasPanelVisible = true;
        }

        private bool IsAnyTargetPanelVisible()
        {
            return IsPanelVisible(_settingsPanel) ||
                   IsPanelVisible(_supplyPanel) ||
                   IsPanelVisible(_backpackPanel);
        }

        private static bool IsPanelVisible(UIPanelShowHide panel)
        {
            return panel != null && panel.IsVisible;
        }

        private void ResetAllAndRequireRelease()
        {
            for (int i = 0; i < _items.Count; i++)
                _items[i]?.Progress.RequireRelease();
        }

        private void UpdateEnergyLabel(float currentEnergy)
        {
            if (_energyLabel == null)
                return;

            float maximumEnergy = _energy != null ? _energy.MaximumEnergy : 0f;
            _energyLabel.text = $"能量 {currentEnergy:F0}/{maximumEnergy:F0}";
        }

        private void OnDestroy()
        {
            _energySubscription?.Dispose();
        }

        private void OnValidate()
        {
            if (_items == null)
                return;

            var keys = new HashSet<KeyCode>();
            for (int i = 0; i < _items.Count; i++)
            {
                MetaballMenuItem item = _items[i];
                if (item == null)
                    continue;

                if (item.HoldKey == KeyCode.None)
                    Debug.LogWarning($"[MetaballMenuController] 子 UI 未配置按键: {item.name}", item);
                else if (!keys.Add(item.HoldKey))
                    Debug.LogWarning($"[MetaballMenuController] 子 UI 使用了重复按键: {item.HoldKey}", item);
            }

            if (_items.Count + 1 > MetaballFieldGraphic.MaxBallCount)
            {
                Debug.LogWarning(
                    $"[MetaballMenuController] 子 UI 数量超过 Shader 球位上限 {MetaballFieldGraphic.MaxBallCount - 1}。",
                    this);
            }
        }
    }
}
