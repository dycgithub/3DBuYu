using System.Collections.Generic;
using CombatSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _Project.UI.Card
{
    /// <summary>把手牌映射到预制体，集中采样可用性并同步卡面与共享操作面板。</summary>
    public sealed class CardHandView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private CardActionPanel actionPanel;
        [SerializeField] private float enterStaggerSeconds = 0.04f;

        private const float AvailabilityRefreshSeconds = 0.25f;
        private readonly Dictionary<int, CardView> _views = new();
        private SkillHand _hand;
        private IObjectResolver _resolver;
        private SkillActivationService _activation;
        private float _nextRefresh;
        private bool _availabilityDirty;
        private bool _subscribed;
        private bool _constructed;

        /// <summary>接收当前 GameLoop 子容器依赖；共享面板由场景自动注入。</summary>
        [Inject]
        private void Construct(SkillHand hand, IObjectResolver resolver, SkillActivationService activation)
        {
            if (_constructed)
            {
                return;
            }

            _hand = hand;
            _resolver = resolver;
            _activation = activation;
            _constructed = true;
            if (isActiveAndEnabled)
            {
                Subscribe();
                RefreshCards();
            }
        }

        /// <summary>重新启用时订阅并同步当前手牌。</summary>
        private void OnEnable()
        {
            Subscribe();
            RefreshCards();
        }

        /// <summary>禁用时释放订阅并结束当前卡牌选择。</summary>
        private void OnDisable()
        {
            Unsubscribe();
            actionPanel?.ApplyAvailability(SkillActivationFailureReason.SkillNotFound);
        }

        /// <summary>定时采样目标和资源条件，离散状态事件合并到同一次刷新。</summary>
        private void Update()
        {
            if (_hand != null && (_availabilityDirty || Time.unscaledTime >= _nextRefresh))
            {
                RefreshAvailability();
            }
        }

        /// <summary>订阅当前手牌的集合与离散状态事件。</summary>
        private void Subscribe()
        {
            if (_hand == null || _subscribed)
            {
                return;
            }
            _hand.SkillsChanged += RefreshCards;
            _hand.SkillStateChanged += MarkAvailabilityDirty;
            _subscribed = true;
        }

        /// <summary>解除订阅，避免场景退出后保留视图引用。</summary>
        private void Unsubscribe()
        {
            if (_hand == null || !_subscribed)
            {
                return;
            }
            _hand.SkillsChanged -= RefreshCards;
            _hand.SkillStateChanged -= MarkAvailabilityDirty;
            _subscribed = false;
        }

        /// <summary>按当前卡牌顺序增删预制体，仅新建或换绑卡牌时更新静态外观。</summary>
        private void RefreshCards()
        {
            if (_hand == null || content == null || cardPrefab == null || _resolver == null)
            {
                return;
            }

            var activeIds = new HashSet<int>();
            for (int index = 0; index < _hand.Skills.Count; index++)
            {
                SkillCardState card = _hand.Skills[index];
                if (!activeIds.Add(card.CardId))
                {
                    continue;
                }
                if (!_views.TryGetValue(card.CardId, out CardView view) || view == null)
                {
                    view = _resolver.Instantiate(cardPrefab, content);
                    _views[card.CardId] = view;
                    view.Selected += SelectCard;
                    view.PlayEnter(index * enterStaggerSeconds);
                }
                view.Bind(card);
                view.transform.SetSiblingIndex(index);
            }

            var removedIds = new List<int>();
            foreach (int cardId in _views.Keys)
            {
                if (!activeIds.Contains(cardId))
                {
                    removedIds.Add(cardId);
                }
            }
            foreach (int cardId in removedIds)
            {
                _views.Remove(cardId, out CardView view);
                if (view != null)
                {
                    view.Selected -= SelectCard;
                    view.ApplyAvailability(SkillActivationFailureReason.SkillNotFound);
                    view.PlayExit(() => Destroy(view.gameObject));
                }
            }
            RefreshAvailability();
        }

        /// <summary>每张卡只查询一次，面板直接复用卡面结果。</summary>
        private void RefreshAvailability()
        {
            _availabilityDirty = false;
            _nextRefresh = Time.unscaledTime + AvailabilityRefreshSeconds;
            foreach (KeyValuePair<int, CardView> pair in _views)
            {
                if (pair.Value != null)
                {
                    pair.Value.ApplyAvailability(_activation != null
                        ? _activation.CheckAvailability(pair.Key)
                        : SkillActivationFailureReason.ActionFailed);
                }
            }

            int? selectedId = actionPanel != null ? actionPanel.SelectedCardId : null;
            if (selectedId.HasValue)
            {
                SkillActivationFailureReason reason = _views.TryGetValue(selectedId.Value, out CardView view) && view != null
                    ? view.Availability : SkillActivationFailureReason.SkillNotFound;
                actionPanel.ApplyAvailability(reason);
            }
        }

        /// <summary>离散状态变化只标脏，不立即重复查询。</summary>
        private void MarkAvailabilityDirty(SkillCardState card) => _availabilityDirty = true;

        /// <summary>选择卡牌时把已经采样的可用性结果传给共享面板。</summary>
        private void SelectCard(SkillCardState card)
        {
            if (_views.TryGetValue(card.CardId, out CardView view) && view != null)
            {
                actionPanel?.Show(card, view.Availability);
            }
        }

        /// <summary>销毁时解除手牌与卡面事件引用。</summary>
        private void OnDestroy()
        {
            Unsubscribe();
            foreach (CardView view in _views.Values)
            {
                if (view != null)
                {
                    view.Selected -= SelectCard;
                }
            }
            _views.Clear();
        }
    }
}
