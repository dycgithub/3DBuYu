using System.Collections.Generic;
using CombatSystem;
using GameSystem;
using Interfaces;
using NUnit.Framework;
using Services;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>验证有序技能数组到独立卡牌状态的组合规则，以及唯一物品显示来源。</summary>
    public sealed class AbilityCardCompositionTests
    {
        private readonly List<Object> _runtimeObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _runtimeObjects.Count - 1; index >= 0; index--)
            {
                if (_runtimeObjects[index] != null)
                {
                    Object.DestroyImmediate(_runtimeObjects[index]);
                }
            }

            _runtimeObjects.Clear();
        }

        [Test]
        public void OneItemTwoSkillsMakesOneCard()
        {
            SkillDefinitionSO firstSkill = CreateSkill("first", 3f);
            SkillDefinitionSO secondSkill = CreateSkill("second", 7f);
            ItemCombatDefinitionSO combat = CreateCombat(firstSkill, energyCost: 0f, useCount: 2);
            ItemDefinition itemDefinition = CreateItem(
                "combo",
                "组合物品",
                combat,
                firstSkill,
                secondSkill);
            ItemVM item = new(itemDefinition);
            SkillHand hand = new();
            CentralSkillLoadout loadout = new(new ItemCombatDefinitionResolver(), hand);

            loadout.ReplaceItems(new[] { item });

            Assert.That(hand.Skills, Has.Count.EqualTo(1));
            SkillCardState card = hand.Skills[0];
            Assert.That(card.SourceItemInstanceId, Is.EqualTo(item.InstanceId));

            TestDamageProcessor damage = new();
            using EnergyService energy = CreateEnergy(20f);
            SkillActivationService service = CreateService(energy, hand, damage, new TestTarget(Vector3.forward));

            SkillActivationResult result = service.TryActivate(card.CardId);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(damage.Requests, Has.Count.EqualTo(2));
            Assert.That(damage.Requests[0].DamageAmount, Is.EqualTo(3f));
            Assert.That(damage.Requests[1].DamageAmount, Is.EqualTo(7f));
        }

        [Test]
        public void CardUsesItemDisplayData()
        {
            SkillDefinitionSO skill = CreateSkill("internal-skill", 10f);
            ItemCombatDefinitionSO combat = CreateCombat(skill, energyCost: 0f, useCount: 1);
            Texture2D iconTexture = new(2, 2);
            _runtimeObjects.Add(iconTexture);
            Sprite icon = Sprite.Create(iconTexture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            _runtimeObjects.Add(icon);
            Color itemColor = new(0.2f, 0.4f, 0.8f, 1f);
            ItemDefinition itemDefinition = CreateItem(
                "display-item",
                "物品标题",
                combat,
                icon,
                itemColor,
                "物品描述",
                skill);
            ItemVM item = new(itemDefinition);
            SkillHand hand = new();
            CentralSkillLoadout loadout = new(new ItemCombatDefinitionResolver(), hand);

            loadout.ReplaceItems(new[] { item });

            SkillCardState card = hand.Skills[0];
            Assert.That(card.SourceItemDefinition, Is.SameAs(itemDefinition));
            Assert.That(card.SourceItemDefinition.DisplayName, Is.EqualTo("物品标题"));
            Assert.That(card.SourceItemDefinition.Description, Is.EqualTo("物品描述"));
            Assert.That(card.SourceItemDefinition.Icon, Is.SameAs(icon));
            Assert.That(card.SourceItemDefinition.Color, Is.EqualTo(itemColor));
        }

        private SkillDefinitionSO CreateSkill(string identifier, float damage)
        {
            SkillDefinitionSO skill = SkillDefinitionSO.CreateRuntimeDefinition(identifier, damage);
            _runtimeObjects.Add(skill);
            return skill;
        }

        private ItemCombatDefinitionSO CreateCombat(
            SkillDefinitionSO skill,
            float energyCost,
            int useCount)
        {
            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Central,
                new SkillGrant(energyCost, useCount, 4f));
            _runtimeObjects.Add(combat);
            return combat;
        }

        private ItemDefinition CreateItem(
            string id,
            string displayName,
            ItemCombatDefinitionSO combat,
            params SkillDefinitionSO[] skills)
        {
            return CreateItem(
                id,
                displayName,
                combat,
                null,
                Color.white,
                string.Empty,
                skills);
        }

        private ItemDefinition CreateItem(
            string id,
            string displayName,
            ItemCombatDefinitionSO combat,
            Sprite icon,
            Color color,
            string description,
            params SkillDefinitionSO[] skills)
        {
            ItemDefinition item = ItemDefinition.CreateRuntime(
                id,
                displayName,
                icon,
                ItemShape.Single,
                combat);
            _runtimeObjects.Add(item);
            AbilityCardTestData.ConfigureItem(item, description, color, skills);
            return item;
        }

        private static EnergyService CreateEnergy(float amount)
        {
            EnergyService energy = new();
            energy.Initialize(amount, amount);
            return energy;
        }

        private static SkillActivationService CreateService(
            EnergyService energy,
            SkillHand hand,
            TestDamageProcessor damage,
            TestTarget target)
        {
            return new SkillActivationService(
                energy,
                new TestCombatPhaseService(),
                damage,
                new TestSpatialQueryService(target),
                hand,
                new TestItemConsumer(),
                new SkillExecutor(damage, new AttackInstanceIdGenerator()));
        }

        private sealed class TestCombatPhaseService : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }

        private sealed class TestItemConsumer : ICombatItemConsumptionService
        {
            public bool CanConsumeItemInstance(int itemInstanceId) => true;

            public bool TryConsumeItemInstance(int itemInstanceId) => true;
        }

        private sealed class TestSpatialQueryService : ISpatialQueryService
        {
            private readonly List<IDamageable> _targets;

            public TestSpatialQueryService(params IDamageable[] targets)
            {
                _targets = new List<IDamageable>(targets);
            }

            public int Register(IDamageable entity, float radius, int layerMask) => -1;
            public void Unregister(int entityId) { }
            public void UpdatePosition(int entityId, Vector3 position) { }
            public IDamageable QueryNearest(Vector3 center, float radius, int layerMask) => null;
            public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask) => _targets;
            public List<IDamageable> QueryAll(int layerMask) => _targets;
        }

        private sealed class TestDamageProcessor : IDamageRequestProcessor
        {
            public readonly List<DamageRequest> Requests = new();

            public bool TryApplyDamage(IDamageable target, in DamageRequest request, out DamageResult result)
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

        private sealed class TestTarget : IDamageable
        {
            public Vector3 Position { get; }
            public bool IsAlive => true;
            public Transform Transform => null;

            public TestTarget(Vector3 position)
            {
                Position = position;
            }
        }
    }

    /// <summary>集中配置 Ability 测试所需的序列化物品和技能字段。</summary>
    internal static class AbilityCardTestData
    {
        public static void ConfigureItem(
            ItemDefinition item,
            string description,
            Color color,
            IReadOnlyList<SkillDefinitionSO> skills)
        {
            SerializedObject serialized = new(item);
            SerializedProperty skillDefinitions = serialized.FindProperty("skillDefinitions");
            Assert.That(
                skillDefinitions,
                Is.Not.Null,
                "ItemDefinition must serialize an ordered SkillDefinitionSO[] named skillDefinitions.");
            skillDefinitions.arraySize = skills?.Count ?? 0;
            for (int index = 0; index < skillDefinitions.arraySize; index++)
            {
                skillDefinitions.GetArrayElementAtIndex(index).objectReferenceValue = skills[index];
            }

            SerializedProperty descriptionProperty = serialized.FindProperty("description");
            Assert.That(descriptionProperty, Is.Not.Null, "ItemDefinition must keep a serialized description field.");
            descriptionProperty.stringValue = description ?? string.Empty;

            SerializedProperty colorProperty = serialized.FindProperty("color");
            Assert.That(colorProperty, Is.Not.Null, "ItemDefinition must keep a serialized color field.");
            colorProperty.colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetTargetMode(
            SkillDefinitionSO skill,
            SkillTargetSelectionMode mode,
            float radius = 0f,
            bool allowNoTarget = false)
        {
            SerializedObject serialized = new(skill);
            SerializedProperty modeProperty = serialized.FindProperty("targetSelectionMode");
            Assert.That(modeProperty, Is.Not.Null, "SkillDefinitionSO must serialize targetSelectionMode.");
            modeProperty.intValue = (int)mode;

            SerializedProperty radiusProperty = serialized.FindProperty("targetRadius");
            Assert.That(radiusProperty, Is.Not.Null, "SkillDefinitionSO must serialize targetRadius.");
            radiusProperty.floatValue = radius;

            SerializedProperty noTargetProperty = serialized.FindProperty("allowNoTarget");
            Assert.That(noTargetProperty, Is.Not.Null, "SkillDefinitionSO must serialize allowNoTarget.");
            noTargetProperty.boolValue = allowNoTarget;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetAction(SkillDefinitionSO skill, SkillActionDefinitionSO action)
        {
            SerializedObject serialized = new(skill);
            SerializedProperty actionProperty = serialized.FindProperty("actionDefinition");
            Assert.That(actionProperty, Is.Not.Null, "SkillDefinitionSO must serialize actionDefinition.");
            actionProperty.objectReferenceValue = action;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
