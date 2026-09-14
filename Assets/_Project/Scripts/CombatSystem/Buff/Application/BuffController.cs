using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace CombatSystem
{
    /// <summary>
    /// 管理单个目标上的活动 Buff、叠加规则、持续时间和属性倍率。
    /// 每个目标只允许一个控制器，确保伤害和复用流程读取同一份 Buff 状态。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BuffController : MonoBehaviour, IPooledObject
    {
        private readonly List<ActiveBuffRuntime> _activeBuffs = new();
        private readonly List<ActiveBuffRuntime> _expiredBuffs = new();

        /// <summary>
        /// 按固定物理时间推进所有限时 Buff 并移除已到期状态。
        /// </summary>
        /// <remarks>
        /// 先收集、后移除是为了避免遍历活动列表时删除元素造成跳过；每个目标各自执行一次更新。
        /// </remarks>
        private void FixedUpdate()
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                ActiveBuffRuntime buff = _activeBuffs[i];
                buff.AdvanceTime(Time.fixedDeltaTime);
            }

            RemoveExpiredBuffs();
        }

        /// <summary>启用目标时从干净状态开始，避免编辑器和对象池复用残留。</summary>
        private void OnEnable()
        {
            ClearAllBuffs();
        }

        /// <summary>禁用目标时清除运行时 Buff，避免对象池复用时残留状态。</summary>
        private void OnDisable()
        {
            ClearAllBuffs();
        }

        /// <summary>对象池租借时清除上一次租借留下的 Buff。</summary>
        public void OnRentFromPool()
        {
            ClearAllBuffs();
        }

        /// <summary>对象池归还时立即清除当前 Buff。</summary>
        public void OnReturnToPool()
        {
            ClearAllBuffs();
        }

        /// <summary>
        /// 清理已经到期的运行时 Buff。
        /// </summary>
        private void RemoveExpiredBuffs()
        {
            _expiredBuffs.Clear();
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].IsExpired)
                {
                    _expiredBuffs.Add(_activeBuffs[i]);
                }
            }

            for (int i = 0; i < _expiredBuffs.Count; i++)
            {
                _activeBuffs.Remove(_expiredBuffs[i]);
            }
        }

        /// <summary>
        /// 以默认来源向目标应用一个 Buff。
        /// </summary>
        /// <param name="definition">要应用的 Buff 定义；为空时忽略。</param>
        public void ApplyBuff(BuffDefinitionSO definition) => ApplyBuff(definition, 0);

        /// <summary>
        /// 以指定来源向目标应用一个 Buff，并按定义的策略处理重复状态。
        /// </summary>
        /// <param name="definition">要应用的 Buff 定义；为空时忽略。</param>
        /// <param name="sourceInstanceId">Buff 来源实例标识，参与查找和按来源移除。</param>
        /// <remarks>
        /// 刷新和加层策略会保留已有定义，只更新时间或层数；替换策略才会更新定义。
        /// 未知策略会按刷新处理，避免新增枚举值时意外创建多个状态，但也要求新增策略同步更新这里。
        /// </remarks>
        public void ApplyBuff(BuffDefinitionSO definition, int sourceInstanceId)
        {
            if (definition == null)
            {
                return;
            }

            RemoveExpiredBuffs();

            ActiveBuffRuntime existing = FindExistingBuff(definition.ModifierType, sourceInstanceId);
            if (existing != null && definition.StackPolicy != BuffStackPolicy.Independent)
            {
                switch (definition.StackPolicy)
                {
                    case BuffStackPolicy.AddStack:
                        existing.StackCount = Mathf.Min(
                            Mathf.Max(1, definition.MaximumStackCount),
                            existing.StackCount + 1);
                        existing.RemainingDurationSeconds = definition.DurationSeconds;
                        break;
                    case BuffStackPolicy.Replace:
                        existing.Definition = definition;
                        existing.StackCount = 1;
                        existing.RemainingDurationSeconds = definition.DurationSeconds;
                        break;
                    default:
                        existing.RemainingDurationSeconds = definition.DurationSeconds;
                        break;
                }
                return;
            }

            _activeBuffs.Add(new ActiveBuffRuntime
            {
                Definition = definition,
                SourceInstanceId = sourceInstanceId,
                RemainingDurationSeconds = definition.DurationSeconds,
                StackCount = 1
            });
        }

        /// <summary>
        /// 获取指定属性类别的活动 Buff 乘法结果。
        /// </summary>
        /// <param name="modifierType">要查询的 Buff 属性类别。</param>
        /// <returns>没有匹配状态时返回一；匹配状态按层数逐次相乘。</returns>
        /// <remarks>
        /// 当前实现把所有属性值都当作乘数处理；需要加法或覆盖语义时必须扩展计算模型，不能只新增枚举值。
        /// </remarks>
        public float GetModifierMultiplier(BuffModifierType modifierType)
        {
            RemoveExpiredBuffs();
            float result = 1f;
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                ActiveBuffRuntime buff = _activeBuffs[i];
                if (buff.Definition == null || buff.Definition.ModifierType != modifierType)
                {
                    continue;
                }

                float value = Mathf.Max(0f, buff.Definition.ModifierValue);
                for (int stack = 0; stack < Mathf.Max(1, buff.StackCount); stack++)
                    result *= value;
            }
            return result;
        }

        /// <summary>
        /// 移除指定来源创建的全部 Buff。
        /// </summary>
        /// <param name="sourceInstanceId">要移除的来源实例标识。</param>
        public void RemoveBySourceId(int sourceInstanceId)
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                if (_activeBuffs[i].SourceInstanceId == sourceInstanceId)
                {
                    _activeBuffs.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 清除当前目标上的全部活动 Buff。
        /// </summary>
        public void ClearAllBuffs()
        {
            _activeBuffs.Clear();
            _expiredBuffs.Clear();
        }

        /// <summary>
        /// 查找指定属性类别和来源的第一个活动 Buff。
        /// </summary>
        /// <param name="modifierType">要匹配的属性类别。</param>
        /// <param name="sourceInstanceId">要匹配的来源实例标识。</param>
        /// <returns>找到时返回活动状态，否则返回 <c>null</c>。</returns>
        private ActiveBuffRuntime FindExistingBuff(BuffModifierType modifierType, int sourceInstanceId)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                ActiveBuffRuntime buff = _activeBuffs[i];
                if (buff.Definition != null &&
                    buff.Definition.ModifierType == modifierType &&
                    buff.SourceInstanceId == sourceInstanceId)
                {
                    return buff;
                }
            }
            return null;
        }
    }
}
