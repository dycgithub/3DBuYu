using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Project.Tests.Buff
{
    /// <summary>
    /// 锁定当前 Buff 与伤害提交 API 的最小公开行为。
    /// </summary>
    public sealed class BuffContractTests
    {
        private readonly List<Object> _objects = new();

        /// <summary>
        /// 销毁测试创建的 Unity 对象，避免状态泄漏到后续测试。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
        }

        /// <summary>
        /// 验证施加的伤害倍率 Buff 会影响对应属性查询。
        /// </summary>
        [Test]
        public void DamageTakenBuff_ChangesDamageMultiplier()
        {
            GameObject owner = new("BuffContractOwner");
            _objects.Add(owner);
            BuffController controller = owner.AddComponent<BuffController>();
            BuffDefinitionSO definition = CreateBuff(
                BuffModifierType.DamageTakenMultiplier,
                0.5f);

            controller.ApplyBuff(definition, 11);

            Assert.That(
                controller.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                Is.EqualTo(0.5f));
        }

        /// <summary>
        /// 验证限时 Buff 按经过时间推进并在到期后失效。
        /// </summary>
        [Test]
        public void TimedBuff_Expires()
        {
            BuffDefinitionSO definition = CreateBuff(
                BuffModifierType.MovementSpeedMultiplier,
                0.5f,
                2f);
            ActiveBuffRuntime state = new()
            {
                Definition = definition,
                RemainingDurationSeconds = definition.DurationSeconds
            };

            state.AdvanceTime(2f);

            Assert.That(state.IsExpired, Is.True);
        }

        /// <summary>
        /// 验证按来源移除只清除指定来源的状态。
        /// </summary>
        [Test]
        public void SourceRemoval_IsScoped()
        {
            GameObject owner = new("BuffSourceContractOwner");
            _objects.Add(owner);
            BuffController controller = owner.AddComponent<BuffController>();
            BuffDefinitionSO definition = CreateBuff(
                BuffModifierType.DamageTakenMultiplier,
                0.5f);

            controller.ApplyBuff(definition, 21);
            controller.ApplyBuff(definition, 22);
            controller.RemoveBySourceId(21);

            Assert.That(
                controller.GetModifierMultiplier(BuffModifierType.DamageTakenMultiplier),
                Is.EqualTo(0.5f));
        }

        /// <summary>
        /// 验证伤害处理器拒绝无效目标且不调用目标接收端。
        /// </summary>
        [Test]
        public void InvalidTarget_IsRejected()
        {
            DamageRequestProcessor processor = new();
            InvalidTarget target = new();
            DamageRequest request = new()
            {
                DamageAmount = 10f,
                DamageType = DamageType.Physical
            };

            bool accepted = processor.TryApplyDamage(target, in request, out DamageResult result);

            Assert.That(accepted, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Invalid));
            Assert.That(target.ReceiveCallCount, Is.Zero);
        }

        /// <summary>
        /// 创建测试用 Buff 定义，并登记其生命周期。
        /// </summary>
        private BuffDefinitionSO CreateBuff(
            BuffModifierType modifierType,
            float modifierValue,
            float durationSeconds = 0f)
        {
            BuffDefinitionSO definition = ScriptableObject.CreateInstance<BuffDefinitionSO>();
            definition.ModifierType = modifierType;
            definition.ModifierValue = modifierValue;
            definition.DurationSeconds = durationSeconds;
            _objects.Add(definition);
            return definition;
        }

        /// <summary>
        /// 只报告无效状态的伤害目标替身。
        /// </summary>
        private sealed class InvalidTarget : IDamageable, IDamageRequestReceiver
        {
            /// <summary>记录处理器是否错误地调用了接收端。</summary>
            public int ReceiveCallCount { get; private set; }

            /// <summary>返回测试目标位置。</summary>
            public Vector3 Position => Vector3.zero;

            /// <summary>无效目标始终不存活。</summary>
            public bool IsAlive => false;

            /// <summary>无效目标没有 Transform。</summary>
            public Transform Transform => null;

            /// <summary>记录不应发生的接收调用。</summary>
            public DamageResult ReceiveDamageRequest(in DamageRequest request)
            {
                ReceiveCallCount++;
                return new DamageResult { Outcome = DamageOutcome.Applied };
            }
        }
    }
}
