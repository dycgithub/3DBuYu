using System;
using System.Collections;
using CombatSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Project.UI.Card
{
    /// <summary>显示当前语言的物品和技能状态；点击只发布选择事件，语言切换不影响技能。</summary>
    [ExecuteAlways]
    public sealed class CardView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI usesText;
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private GameObject unavailableOverlay;
        [SerializeField] private CanvasGroup canvasGroup;

        private SkillCardState _skill;
        private string _baseDescription;
        private SkillActivationFailureReason? _availability;
        private Coroutine _animation;

        /// <summary>卡牌被点击时发布绑定技能，不直接触发技能激活。</summary>
        public event Action<SkillCardState> Selected;
        public SkillCardState Skill => _skill;
        public SkillActivationFailureReason Availability => _availability ?? SkillActivationFailureReason.SkillNotFound;
        public RectTransform RectTransform => transform as RectTransform;

        /// <summary>缓存视图组件并绑定唯一点击入口。</summary>
        private void Awake()
        {
            button ??= GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }

            canvasGroup ??= GetComponent<CanvasGroup>();
            LocalizationSettings.SelectedLocaleChanged += RefreshLanguage;
        }

        /// <summary>重读物品译文和可用性提示，不重新绑定或消耗技能。</summary>
        private void RefreshLanguage(Locale _)
        {
            if (_skill == null)
            {
                return;
            }

            ItemDefinition item = _skill.SourceItemDefinition;
            if (titleText != null)
            {
                titleText.text = item?.DisplayName ?? string.Empty;
            }

            _baseDescription = item?.Description ?? string.Empty;
            var reason = Availability;
            _availability = null;
            ApplyAvailability(reason);
        }

        /// <summary>首次绑定卡牌时写入静态物品外观；同一实例不会因状态刷新重复绑定。</summary>
        public void Bind(SkillCardState skill)
        {
            if (_skill == skill)
            {
                return;
            }
            _skill = skill;
            if (skill == null)
            {
                return;
            }

            ItemDefinition item = skill.SourceItemDefinition;

            if (background != null)
            {
                background.color = item != null ? item.Color : Color.white;
            }

            if (icon != null)
            {
                icon.sprite = item?.Icon;
                icon.enabled = item?.Icon != null;
            }

            if (titleText != null)
            {
                titleText.text = item?.DisplayName ?? string.Empty;
            }

            _baseDescription = item?.Description ?? string.Empty;
            _availability = null;
            RefreshStatus();
            ApplyAvailability(SkillActivationFailureReason.SkillNotFound);
        }

        /// <summary>仅在结果变化时更新按钮、遮罩与提示，不发起战斗查询。</summary>
        public void ApplyAvailability(SkillActivationFailureReason reason)
        {
            if (_skill == null)
            {
                return;
            }
            RefreshStatus();
            if (_availability == reason)
            {
                return;
            }
            _availability = reason;
            bool available = reason == SkillActivationFailureReason.None;
            if (button != null)
            {
                button.interactable = available;
            }

            if (unavailableOverlay != null)
            {
                unavailableOverlay.SetActive(!available);
            }
            if (descriptionText != null)
            {
                string message = reason switch
                {
                    SkillActivationFailureReason.InsufficientEnergy => L10n.Get("card.insufficientEnergy"),
                    SkillActivationFailureReason.NoValidTarget => L10n.Get("card.noTarget"),
                    SkillActivationFailureReason.InvalidTarget => L10n.Get("card.invalidTarget"),
                    SkillActivationFailureReason.CooldownActive => L10n.Get("card.cooldownActive"),
                    SkillActivationFailureReason.UsesExhausted => L10n.Get("card.usesExhausted"),
                    SkillActivationFailureReason.CombatActionsNotAllowed => L10n.Get("card.actionsBlocked"),
                    SkillActivationFailureReason.SourceItemUnavailable => L10n.Get("card.itemUnavailable"),
                    SkillActivationFailureReason.SourceConsumptionFailed => L10n.Get("card.consumptionFailed"),
                    SkillActivationFailureReason.ActionFailed => L10n.Get("card.actionFailed"),
                    _ => string.Empty
                };
                descriptionText.text = _baseDescription +
                    (message.Length > 0 ? "\n" + message : string.Empty);
            }
        }

        /// <summary>刷新卡牌消费配置和当前剩余状态。</summary>
        private void RefreshStatus()
        {
            if (_skill == null)
            {
                return;
            }

            if (energyText != null)
            {
                energyText.text = L10n.Get("card.energy", _skill.EnergyCost);
            }

            if (usesText != null)
            {
                usesText.text = L10n.Get("card.uses", _skill.RemainingUses);
            }

            if (cooldownText != null)
            {
                cooldownText.text = L10n.Get("card.cooldown", _skill.RemainingCooldownSeconds, _skill.CooldownSeconds);
            }
        }

        /// <summary>缺少 Button 时使用左键指针作为点击入口。</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (button == null && eventData != null &&
                eventData.button == PointerEventData.InputButton.Left)
            {
                HandleClick();
            }
        }

        /// <summary>播放入场反馈并停止上一段表现协程。</summary>
        public void PlayEnter(float delaySeconds)
        {
            if (_animation != null)
            {
                StopCoroutine(_animation);
            }

            _animation = StartCoroutine(EnterRoutine(Mathf.Max(0f, delaySeconds)));
        }

        /// <summary>播放退场反馈，结束后通知手牌视图释放对象。</summary>
        public void PlayExit(System.Action completed)
        {
            if (_animation != null)
            {
                StopCoroutine(_animation);
            }

            _animation = StartCoroutine(ExitRoutine(completed));
        }

        /// <summary>仅对可用且已绑定的卡牌发布选择。</summary>
        private void HandleClick()
        {
            if (_skill == null || Availability != SkillActivationFailureReason.None)
            {
                return;
            }

            Selected?.Invoke(_skill);
        }

        /// <summary>按不受暂停影响的时间依次显示卡牌。</summary>
        private IEnumerator EnterRoutine(float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(delaySeconds);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        /// <summary>淡出卡牌并在结束时回调。</summary>
        private IEnumerator ExitRoutine(System.Action completed)
        {
            if (canvasGroup != null)
            {
                float start = canvasGroup.alpha;
                float elapsed = 0f;
                while (elapsed < 0.15f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(start, 0f, elapsed / 0.15f);
                    yield return null;
                }

                canvasGroup.alpha = 0f;
            }

            completed?.Invoke();
        }

        /// <summary>销毁时解绑按钮监听。</summary>
        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= RefreshLanguage;
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }
    }
}
