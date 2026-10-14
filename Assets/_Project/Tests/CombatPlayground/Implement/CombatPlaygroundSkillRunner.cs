using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using UnityEngine;
using VContainer;
using _Project.Tests.CombatPlayground.Model;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>为测试技能建立合成卡牌，并选择正式释放或动作直测入口。</summary>
    public sealed class CombatPlaygroundSkillRunner : MonoBehaviour
    {
        private const float TestEnergyCost = 10f;
        private const int TestUseCount = 20;
        private const float TestCooldownSeconds = 2f;

        [SerializeField] private SkillDefinitionSO[] skills;

        [Inject] private ISkillExecutionService _executor;
        [Inject] private SkillActivationService _activationService;
        [Inject] private SkillHand _skillHand;
        [Inject] private CombatPlaygroundInventory _inventory;

        private readonly List<SkillCardState> _cards = new();

        /// <summary>当前场景技能数组长度，供视图绑定按钮。</summary>
        internal int SkillCount => skills?.Length ?? 0;

        /// <summary>场景启动时建立正式释放所需的测试手牌。</summary>
        private void Start()
        {
            ResetSkillState();
        }

        /// <summary>按当前配置重建技能卡和合成物品状态。</summary>
        internal void ResetSkillState()
        {
            _skillHand?.Clear();
            _inventory?.Clear();
            _cards.Clear();
            if (skills == null)
            {
                return;
            }

            for (int index = 0; index < skills.Length; index++)
            {
                SkillDefinitionSO definition = skills[index];
                if (definition == null)
                {
                    _cards.Add(null);
                    continue;
                }

                int sourceId = CreateSourceId(index);
                _inventory?.RegisterItem(sourceId);
                SkillCardState card = SkillCardState.Create(
                    sourceId,
                    null,
                    new SkillGrant(TestEnergyCost, TestUseCount, TestCooldownSeconds),
                    new[] { definition });
                _cards.Add(card);
            }

            _skillHand?.Rebuild(_cards);
        }

        /// <summary>执行当前索引技能，并返回供场景事件日志显示的结果。</summary>
        internal string Execute(
            int index,
            Vector3 sourcePosition,
            IReadOnlyList<IDamageable> targets,
            CombatPlaygroundSkillExecutionMode mode)
        {
            if (!IsSkillAvailable(index))
            {
                return "技能失败：技能配置不可用";
            }

            SkillCardState card = _cards[index];
            if (mode == CombatPlaygroundSkillExecutionMode.FormalRelease)
            {
                if (_activationService == null)
                {
                    return "技能失败：正式释放服务未注册";
                }

                SkillActivationResult activation = _activationService.TryActivate(card.CardId);
                return activation.Succeeded
                    ? $"正式释放成功：{skills[index].SkillIdentifier}，剩余使用 {activation.RemainingUses} 次"
                    : $"正式释放失败：{activation.FailureReason}";
            }

            if (_executor == null)
            {
                return "技能失败：动作执行服务未注册";
            }

            SkillExecutionRequest request = new(
                skills[index],
                card.SourceItemInstanceId,
                sourcePosition,
                targets);
            SkillExecutionResult result = _executor.Execute(in request);
            return result.Succeeded
                ? $"动作直测成功：{skills[index].SkillIdentifier}，目标 {result.AppliedTargetCount} 个"
                : $"动作直测失败：{result.FailureReason}";
        }

        private bool IsSkillAvailable(int index)
        {
            return skills != null && index >= 0 && index < skills.Length &&
                skills[index] != null && index < _cards.Count && _cards[index] != null;
        }

        private int CreateSourceId(int index)
        {
            unchecked
            {
                int sourceId = (GetInstanceID() * 397) ^ index;
                return sourceId != 0 ? sourceId : index + 1;
            }
        }
    }
}
