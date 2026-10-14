using CombatSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace _Project.UI.Card
{
    /// <summary>显示当前语言的选中卡操作，复用手牌可用性结果，释放时重新提交业务校验。</summary>
    public sealed class CardActionPanel : MonoBehaviour
    {
        [SerializeField] private Button releaseButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text releaseText;
        [SerializeField] private TMP_Text cancelText;
        [SerializeField] private UIButtonStateView releaseStateView;

        [Inject] private SkillActivationService _skillActivationService;

        private SkillCardState _selectedSkill;
        private bool _canRelease;
        private bool _initialized;

        /// <summary>当前共享操作面板是否可见。</summary>
        public bool IsVisible => gameObject.activeSelf;
        /// <summary>供手牌协调器同步选择有效性的卡牌身份。</summary>
        public int? SelectedCardId => _selectedSkill?.CardId;

        /// <summary>初始化序列化按钮和初始交互状态。</summary>
        private void Awake()
        {
            Initialize();
            if (_selectedSkill == null)
            {
                SetReleaseInteractable(false);
            }
        }

        /// <summary>显示当前卡牌的释放和取消操作。</summary>
        /// <param name="skill">当前选中的卡牌。</param>
        /// <param name="reason">手牌协调器提供的可用性结果。</param>
        public void Show(SkillCardState skill, SkillActivationFailureReason reason)
        {
            Initialize();
            if (skill == null || reason == SkillActivationFailureReason.SkillNotFound)
            {
                Hide();
                return;
            }

            _selectedSkill = skill;
            SetLabels();
            ApplyAvailability(reason);
            gameObject.SetActive(true);
        }

        /// <summary>只绑定一次已有按钮，不创建界面对象。</summary>
        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            releaseButton ??= transform.Find("ReleaseButton")?.GetComponent<Button>();
            cancelButton ??= transform.Find("CancelButton")?.GetComponent<Button>();
            releaseText ??= releaseButton?.GetComponentInChildren<TMP_Text>(true);
            cancelText ??= cancelButton?.GetComponentInChildren<TMP_Text>(true);
            releaseStateView ??= releaseButton?.GetComponent<UIButtonStateView>();

            releaseButton?.onClick.AddListener(OnReleaseClicked);
            cancelButton?.onClick.AddListener(OnCancelClicked);
            LocalizationSettings.SelectedLocaleChanged += RefreshLanguage;
            _initialized = true;
        }

        /// <summary>更新已选卡的交互状态；卡牌移除时清空选择并隐藏面板。</summary>
        public void ApplyAvailability(SkillActivationFailureReason reason)
        {
            if (_selectedSkill == null || reason == SkillActivationFailureReason.SkillNotFound)
            {
                Hide();
                return;
            }
            _canRelease = reason == SkillActivationFailureReason.None;
            SetReleaseInteractable(_canRelease);
        }

        /// <summary>设置共享面板操作文字。</summary>
        private void SetLabels()
        {
            if (releaseText != null)
            {
                releaseText.text = L10n.Get("ui.cast");
            }

            if (cancelText != null)
            {
                cancelText.text = L10n.Get("ui.cancel");
            }
        }

        /// <summary>语言变化只更新按钮文字。</summary>
        private void RefreshLanguage(Locale _)
        {
            SetLabels();
        }

        /// <summary>把业务可用性传给现有按钮表现。</summary>
        private void SetReleaseInteractable(bool interactable)
        {
            if (releaseStateView != null)
            {
                releaseStateView.SetInteractable(interactable);
                return;
            }

            if (releaseButton != null)
            {
                releaseButton.interactable = interactable;
            }
        }

        /// <summary>点击时调用最新释放规则，不以之前的可用性缓存代替实际校验。</summary>
        private void OnReleaseClicked()
        {
            if (!_canRelease || _selectedSkill == null || _skillActivationService == null)
            {
                return;
            }

            SkillActivationResult result = _skillActivationService.TryActivate(_selectedSkill.CardId);
            if (result.Succeeded)
            {
                Hide();
                return;
            }

            ApplyAvailability(result.FailureReason);
        }

        /// <summary>取消选择而不执行技能。</summary>
        private void OnCancelClicked()
        {
            Hide();
        }

        /// <summary>清空选择和交互状态并关闭面板。</summary>
        private void Hide()
        {
            _selectedSkill = null;
            _canRelease = false;
            SetReleaseInteractable(false);
            gameObject.SetActive(false);
        }

        /// <summary>销毁时移除按钮监听。</summary>
        private void OnDestroy()
        {
            if (!_initialized)
            {
                return;
            }

            releaseButton?.onClick.RemoveListener(OnReleaseClicked);
            cancelButton?.onClick.RemoveListener(OnCancelClicked);
            LocalizationSettings.SelectedLocaleChanged -= RefreshLanguage;
        }
    }
}
