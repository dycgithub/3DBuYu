using System;
using System.Collections.Generic;
using System.Reflection;
using CombatSystem;
using GameSystem;
using Interfaces;
using NUnit.Framework;
using Play;
using Services;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>锁定中央背包五类主动技能的执行、目标和作用域契约。</summary>
    public sealed class CentralItemSkillTests
    {
        private readonly List<UnityEngine.Object> _objects = new();

        /// <summary>释放测试创建的 Unity 对象。</summary>
        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
        }

        /// <summary>中央动作经由统一技能执行器收到自己的上下文并完成调用。</summary>
        [Test]
        public void CentralAction_ExecutesThroughSkillExecutor()
        {
            CentralSkillActionDefinitionSO action = CreateAction(CentralSkillActionKind.PushEnemies);
            SkillDefinitionSO definition = CreateSkill(
                "010",
                action,
                SkillTargetSelectionMode.AroundCaster,
                3f,
                false,
                0f);
            RecordingCentralSkillEffectService effects = new();
            SkillExecutor executor = new(null, new AttackInstanceIdGenerator(), effects);
            TestMotionTarget target = new(Vector3.right);

            SkillExecutionResult result = executor.Execute(
                definition,
                sourceInstanceId: 15,
                sourcePosition: Vector3.zero,
                targets: new[] { (IDamageable)target });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(effects.PushCalls, Is.EqualTo(1));
            Assert.That(effects.LastTargets, Has.Count.EqualTo(1));
            Assert.That(effects.LastTargets[0], Is.SameAs(target));
        }

        /// <summary>动作失败时，整卡不消费能量、次数、物品，也不提交伤害。</summary>
        [Test]
        public void FailedAction_DoesNotConsumeCard()
        {
            FailingActionSO action = Track(ScriptableObject.CreateInstance<FailingActionSO>());
            SkillDefinitionSO skill = CreateSkill(
                "failed-central",
                action,
                SkillTargetSelectionMode.AllLivingEnemies,
                0f,
                false,
                5f);
            ItemCombatDefinitionSO combat = CreateCombat(CombatScope.Central, new SkillGrant(3f, 2, 4f));
            ItemDefinition itemDefinition = CreateItem("012", combat, skill);
            ItemVM item = new(itemDefinition);
            SkillHand hand = new();
            new CentralSkillLoadout(new ItemCombatDefinitionResolver(), hand).ReplaceItems(new[] { item });
            SkillCardState card = hand.Skills[0];
            RecordingDamageProcessor damage = new();
            RecordingItemConsumer consumer = new();
            TestEnergyService energy = new(10f);
            SkillActivationService service = new(
                energy,
                new CombatPhase(),
                damage,
                new SpatialQuery(new TestMotionTarget(Vector3.forward)),
                hand,
                consumer,
                new SkillExecutor(damage, new AttackInstanceIdGenerator()));

            SkillActivationResult result = service.TryActivate(card.CardId);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SkillActivationFailureReason.ActionFailed));
            Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(10f));
            Assert.That(card.RemainingUses, Is.EqualTo(2));
            Assert.That(card.RemainingCooldownSeconds, Is.Zero);
            Assert.That(consumer.ConsumeCount, Is.Zero);
            Assert.That(damage.Requests, Is.Empty);
        }

        /// <summary>全体目标由查询结果复制成快照，不受查询缓存下一次复用影响。</summary>
        [Test]
        public void AllLivingTargets_AreCopiedToSnapshot()
        {
            SkillDefinitionSO definition = CreateSkill(
                "012",
                null,
                SkillTargetSelectionMode.AllLivingEnemies,
                0f,
                false,
                100f);
            TestMotionTarget first = new(Vector3.left);
            TestMotionTarget second = new(Vector3.right);
            MutableSpatialQuery query = new(first, second);
            SkillTargetResolver resolver = new(query);

            Assert.That(
                resolver.TryResolve(definition, Vector3.zero, out IReadOnlyList<IDamageable> snapshot, out _),
                Is.True);

            query.Targets.Clear();

            Assert.That(snapshot, Has.Count.EqualTo(2));
            Assert.That(snapshot[0], Is.SameAs(first));
            Assert.That(snapshot[1], Is.SameAs(second));
        }

        /// <summary>范围目标只保留半径内对象，并同样脱离空间查询缓存。</summary>
        [Test]
        public void AroundCasterTargets_AreFilteredAndCopiedToSnapshot()
        {
            SkillDefinitionSO definition = CreateSkill(
                "010",
                null,
                SkillTargetSelectionMode.AroundCaster,
                2f,
                false,
                0f);
            TestMotionTarget near = new(Vector3.right);
            TestMotionTarget far = new(Vector3.right * 5f);
            MutableSpatialQuery query = new(near, far);
            SkillTargetResolver resolver = new(query);

            Assert.That(
                resolver.TryResolve(definition, Vector3.zero, out IReadOnlyList<IDamageable> snapshot, out _),
                Is.True);

            query.Targets.Clear();

            Assert.That(snapshot, Has.Count.EqualTo(1));
            Assert.That(snapshot[0], Is.SameAs(near));
        }

        /// <summary>001 通过中央核心现有的 TryExpandPort 解锁下一个端口，且无需敌人目标。</summary>
        [Test]
        public void UnlockNextTransmitter_UsesCentralCoreExpansion()
        {
            GameObject coreObject = Track(new GameObject("CentralSkillCore"));
            CentralCore core = coreObject.AddComponent<CentralCore>();
            CentralSO central = Track(ScriptableObject.CreateInstance<CentralSO>());
            TransmitterSO transmitter = Track(ScriptableObject.CreateInstance<TransmitterSO>());
            transmitter.portName = "LockedTransmitter";
            transmitter.portId = "locked-transmitter";
            transmitter.isInitiallyLocked = true;
            central.Transmitters = new[] { transmitter };
            SetPrivateField(core, "centralSo", central);
            InvokePrivateMethod(core, "Start");

            CentralSkillActionDefinitionSO action = CreateAction(CentralSkillActionKind.UnlockNextTransmitter);
            SkillDefinitionSO definition = CreateSkill(
                "001",
                action,
                SkillTargetSelectionMode.AllLivingEnemies,
                0f,
                true,
                0f);
            SkillExecutor executor = new(
                null,
                new AttackInstanceIdGenerator(),
                new CentralSkillEffectService(core, null));

            SkillExecutionResult result = executor.Execute(
                definition,
                sourceInstanceId: 1,
                sourcePosition: Vector3.zero,
                targets: Array.Empty<IDamageable>());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(core.ActivePortCount, Is.EqualTo(1));
            Assert.That(core.GetPort(0).IsLocked, Is.False);
        }

        /// <summary>013 将永久减速倍率提交给每个范围快照中的运动目标。</summary>
        [Test]
        public void SlowAllEnemies_SetsPermanentHalfSpeedMultiplier()
        {
            CentralSkillActionDefinitionSO action = CreateAction(CentralSkillActionKind.SlowAllEnemies);
            SkillDefinitionSO definition = CreateSkill(
                "013",
                action,
                SkillTargetSelectionMode.AllLivingEnemies,
                0f,
                false,
                0f);
            TestMotionTarget first = new(Vector3.left);
            TestMotionTarget second = new(Vector3.right);
            SkillExecutor executor = new(
                null,
                new AttackInstanceIdGenerator(),
                new CentralSkillEffectService(null, null));

            SkillExecutionResult result = executor.Execute(
                definition,
                sourceInstanceId: 13,
                sourcePosition: Vector3.zero,
                targets: new IDamageable[] { first, second });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(first.SpeedMultiplier, Is.EqualTo(0.5f));
            Assert.That(second.SpeedMultiplier, Is.EqualTo(0.5f));
        }

        /// <summary>012 对全体目标快照各提交 100 点伤害。</summary>
        [Test]
        public void DamageAllEnemies_AppliesOneHundredToEveryTarget()
        {
            CentralSkillActionDefinitionSO action = CreateAction(CentralSkillActionKind.DamageAllEnemies);
            SkillDefinitionSO definition = CreateSkill(
                "012",
                action,
                SkillTargetSelectionMode.AllLivingEnemies,
                0f,
                false,
                100f);
            RecordingDamageProcessor damage = new();
            SkillExecutor executor = new(damage, new AttackInstanceIdGenerator());
            TestMotionTarget first = new(Vector3.left);
            TestMotionTarget second = new(Vector3.right);

            SkillExecutionResult result = executor.Execute(
                definition,
                sourceInstanceId: 12,
                sourcePosition: Vector3.zero,
                targets: new IDamageable[] { first, second });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.AppliedTargetCount, Is.EqualTo(2));
            Assert.That(damage.Requests, Has.Count.EqualTo(2));
            Assert.That(damage.Requests[0].DamageAmount, Is.EqualTo(100f));
            Assert.That(damage.Requests[1].DamageAmount, Is.EqualTo(100f));
        }

        /// <summary>015 只向全局发射器效果边界提交 2 倍倍率，不改写发射器配置。</summary>
        [Test]
        public void DoubleTransmitterFireRate_SetsGlobalMultiplier()
        {
            CentralSkillActionDefinitionSO action = CreateAction(
                CentralSkillActionKind.DoubleTransmitterFireRate);
            SkillDefinitionSO definition = CreateSkill(
                "015",
                action,
                SkillTargetSelectionMode.AllLivingEnemies,
                0f,
                true,
                0f);
            RecordingGlobalTransmitterEffectService globalEffects = new();
            SkillExecutor executor = new(
                null,
                new AttackInstanceIdGenerator(),
                new CentralSkillEffectService(null, globalEffects));

            SkillExecutionResult result = executor.Execute(
                definition,
                sourceInstanceId: 15,
                sourcePosition: Vector3.zero,
                targets: Array.Empty<IDamageable>());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(globalEffects.SetCalls, Is.EqualTo(1));
            Assert.That(globalEffects.Multiplier, Is.EqualTo(2f));
        }

        /// <summary>中央技能卡只从 Central 或 CentralAndTransmitter 作用域生成。</summary>
        [Test]
        public void CentralSkillLoadout_RejectsTransmitterOnlyCards()
        {
            SkillDefinitionSO skill = CreateSkill(
                "transmitter-only-skill",
                null,
                SkillTargetSelectionMode.AllLivingEnemies,
                0f,
                true,
                0f);
            ItemCombatDefinitionSO transmitterCombat = CreateCombat(
                CombatScope.Transmitter,
                new SkillGrant(0f, 1, 0f));
            ItemDefinition transmitterItemDefinition = CreateItem(
                "014",
                transmitterCombat,
                skill);
            SkillHand hand = new();

            new CentralSkillLoadout(new ItemCombatDefinitionResolver(), hand).ReplaceItems(
                new[] { new ItemVM(transmitterItemDefinition) });

            Assert.That(hand.Skills, Is.Empty);
        }

        /// <summary>创建带动作和目标契约的临时技能资产。</summary>
        private SkillDefinitionSO CreateSkill(
            string identifier,
            SkillActionDefinitionSO action,
            SkillTargetSelectionMode targetMode,
            float radius,
            bool allowNoTarget,
            float damage)
        {
            SkillDefinitionSO definition = Track(SkillDefinitionSO.CreateRuntimeDefinition(identifier, damage));
            SerializedObject serialized = new(definition);
            serialized.FindProperty("targetSelectionMode").intValue = (int)targetMode;
            serialized.FindProperty("targetRadius").floatValue = radius;
            serialized.FindProperty("allowNoTarget").boolValue = allowNoTarget;
            serialized.FindProperty("actionDefinition").objectReferenceValue = action;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        /// <summary>创建带默认数值契约的临时中央动作资产。</summary>
        private CentralSkillActionDefinitionSO CreateAction(CentralSkillActionKind kind)
        {
            return Track(CentralSkillActionDefinitionSO.CreateRuntime(kind));
        }

        /// <summary>创建临时物品战斗配置。</summary>
        private ItemCombatDefinitionSO CreateCombat(CombatScope scope, SkillGrant grant)
        {
            return Track(ItemCombatDefinitionSO.CreateRuntime(scope, grant));
        }

        /// <summary>创建带中央技能数组的临时物品定义。</summary>
        private ItemDefinition CreateItem(
            string identifier,
            ItemCombatDefinitionSO combat,
            params SkillDefinitionSO[] skills)
        {
            ItemDefinition definition = Track(ItemDefinition.CreateRuntime(
                identifier,
                identifier,
                null,
                ItemShape.Single,
                combat));
            SerializedObject serialized = new(definition);
            SerializedProperty skillDefinitions = serialized.FindProperty("skillDefinitions");
            skillDefinitions.arraySize = skills.Length;
            for (int index = 0; index < skills.Length; index++)
            {
                skillDefinitions.GetArrayElementAtIndex(index).objectReferenceValue = skills[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        /// <summary>登记临时对象。</summary>
        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _objects.Add(value);
            return value;
        }

        /// <summary>设置测试所需的私有配置字段。</summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field?.SetValue(target, value);
        }

        /// <summary>调用中央核心的私有初始化入口，保持生产初始化路径不变。</summary>
        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method?.Invoke(target, null);
        }

        private sealed class FailingActionSO : SkillActionDefinitionSO
        {
            /// <summary>明确拒绝动作以验证整卡事务回滚。</summary>
            public override bool TryExecute(
                in SkillExecutionContext context,
                IReadOnlyList<IDamageable> targets,
                IDamageRequestProcessor damageProcessor,
                out int appliedTargetCount)
            {
                appliedTargetCount = 0;
                return false;
            }
        }

        private sealed class RecordingCentralSkillEffectService : ICentralSkillEffectService
        {
            public int PushCalls { get; private set; }
            public IReadOnlyList<IDamageable> LastTargets { get; private set; }

            /// <summary>记录解锁调用。</summary>
            public bool TryUnlockNextTransmitter() => true;

            /// <summary>记录范围击退调用。</summary>
            public bool TryPushEnemies(
                IReadOnlyList<IDamageable> targets,
                Vector3 sourcePosition,
                float impulseStrength)
            {
                PushCalls++;
                LastTargets = targets == null ? null : new List<IDamageable>(targets);
                return true;
            }

            /// <summary>记录减速调用。</summary>
            public bool TrySlowEnemies(
                IReadOnlyList<IDamageable> targets,
                float multiplier,
                int sourceInstanceId)
            {
                return true;
            }

            /// <summary>记录全局射速调用。</summary>
            public bool TrySetTransmitterAttackSpeedMultiplier(
                float multiplier,
                int sourceInstanceId)
            {
                return true;
            }
        }

        private sealed class RecordingGlobalTransmitterEffectService : ITransmitterGlobalEffectService
        {
            public float AttackSpeedMultiplier { get; private set; } = 1f;
            public float Multiplier => AttackSpeedMultiplier;
            public int SetCalls { get; private set; }

            /// <summary>保存中央技能提交的全局倍率。</summary>
            public void SetAttackSpeedMultiplier(float multiplier, int sourceInstanceId)
            {
                SetCalls++;
                AttackSpeedMultiplier = multiplier;
            }

            /// <summary>清除指定来源的永久效果。</summary>
            public void ClearSource(int sourceInstanceId)
            {
                AttackSpeedMultiplier = 1f;
            }
        }

        private sealed class TestMotionTarget : IDamageable, IEnemyMotionEffectTarget
        {
            public Vector3 Position { get; }
            public bool IsAlive => true;
            public Transform Transform => null;
            public float SpeedMultiplier { get; private set; } = 1f;
            public Vector3 Impulse { get; private set; }

            public TestMotionTarget(Vector3 position)
            {
                Position = position;
            }

            /// <summary>保存运动速度倍率。</summary>
            public void SetMovementSpeedMultiplier(float multiplier)
            {
                SpeedMultiplier = multiplier;
            }

            /// <summary>保存外部冲量。</summary>
            public void AddExternalImpulse(Vector3 impulse)
            {
                Impulse = impulse;
            }
        }

        private sealed class RecordingDamageProcessor : IDamageRequestProcessor
        {
            public List<DamageRequest> Requests { get; } = new();

            /// <summary>记录伤害请求并返回已应用结果。</summary>
            public bool TryApplyDamage(
                IDamageable target,
                in DamageRequest request,
                out DamageResult result)
            {
                Requests.Add(request);
                result = new DamageResult
                {
                    Outcome = DamageOutcome.Applied,
                    AppliedDamage = request.DamageAmount
                };
                return true;
            }
        }

        private sealed class MutableSpatialQuery : ISpatialQueryService
        {
            public List<IDamageable> Targets { get; } = new();

            public MutableSpatialQuery(params IDamageable[] targets)
            {
                Targets.AddRange(targets);
            }

            /// <summary>不参与测试的空间注册入口。</summary>
            public int Register(IDamageable entity, float radius, int layerMask) => -1;

            /// <summary>不参与测试的空间注销入口。</summary>
            public void Unregister(int entityId) { }

            /// <summary>不参与测试的位置更新入口。</summary>
            public void UpdatePosition(int entityId, Vector3 position) { }

            /// <summary>按统一查询接口返回当前缓存。</summary>
            public IDamageable QueryNearest(Vector3 center, float radius, int layerMask) => null;

            /// <summary>按统一查询接口返回当前缓存。</summary>
            public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask)
                => Targets;

            /// <summary>按统一查询接口返回当前缓存。</summary>
            public List<IDamageable> QueryAll(int layerMask) => Targets;
        }

        private sealed class SpatialQuery : ISpatialQueryService
        {
            private readonly List<IDamageable> _targets;

            public SpatialQuery(params IDamageable[] targets)
            {
                _targets = new List<IDamageable>(targets);
            }

            /// <summary>不参与测试的空间注册入口。</summary>
            public int Register(IDamageable entity, float radius, int layerMask) => -1;

            /// <summary>不参与测试的空间注销入口。</summary>
            public void Unregister(int entityId) { }

            /// <summary>不参与测试的位置更新入口。</summary>
            public void UpdatePosition(int entityId, Vector3 position) { }

            /// <summary>不参与测试的最近目标入口。</summary>
            public IDamageable QueryNearest(Vector3 center, float radius, int layerMask) => null;

            /// <summary>返回固定范围目标。</summary>
            public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask)
                => _targets;

            /// <summary>返回固定全体目标。</summary>
            public List<IDamageable> QueryAll(int layerMask) => _targets;
        }

        private sealed class CombatPhase : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }

        private sealed class RecordingItemConsumer : ICombatItemConsumptionService
        {
            public int ConsumeCount { get; private set; }

            /// <summary>允许前置检查消费来源。</summary>
            public bool CanConsumeItemInstance(int itemInstanceId) => true;

            /// <summary>记录实际消费。</summary>
            public bool TryConsumeItemInstance(int itemInstanceId)
            {
                ConsumeCount++;
                return true;
            }
        }

        private sealed class TestEnergyService : IEnergyService
        {
            private readonly EnergyService _inner = new();

            public TestEnergyService(float energy)
            {
                _inner.Initialize(energy, energy);
            }

            public R3.ReadOnlyReactiveProperty<float> CurrentEnergy => _inner.CurrentEnergy;
            public float MaximumEnergy => _inner.MaximumEnergy;
            public bool IsDepleted => _inner.IsDepleted;
            public event Action EnergyDepleted
            {
                add => _inner.EnergyDepleted += value;
                remove => _inner.EnergyDepleted -= value;
            }

            /// <summary>初始化测试能量。</summary>
            public void Initialize(float initialEnergy, float maximumEnergy)
                => _inner.Initialize(initialEnergy, maximumEnergy);

            /// <summary>转发能量消费。</summary>
            public bool TrySpend(float baseAmount, EnergySpendKind kind)
                => _inner.TrySpend(baseAmount, kind);

            /// <summary>转发能量扣除。</summary>
            public float Drain(float baseAmount, EnergySpendKind kind)
                => _inner.Drain(baseAmount, kind);

            /// <summary>转发能量增加。</summary>
            public float AddEnergy(float amount) => _inner.AddEnergy(amount);

            /// <summary>转发时间推进。</summary>
            public float Tick(float deltaTime, float baseDrainPerSecond)
                => _inner.Tick(deltaTime, baseDrainPerSecond);
        }
    }
}
