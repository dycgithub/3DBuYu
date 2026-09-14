using System;
using System.Collections.Generic;
using System.Reflection;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using Services;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using GameSystem;
using _Project.UI.Card;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证卡牌只显示和选择，共享面板接收可用性结果并在释放时重新校验。
    /// </summary>
    public sealed class CardActionPanelContractTests
    {
        private const string CardPrefabPath = "Assets/_Project/Prefabs/UI/Card/CardView.prefab";

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<IDisposable> _disposables = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _disposables.Count - 1; index >= 0; index--)
            {
                _disposables[index].Dispose();
            }

            _disposables.Clear();

            for (int index = _createdObjects.Count - 1; index >= 0; index--)
            {
                if (_createdObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void CardClick_SelectsCardWithoutActivatingSkill()
        {
            SkillFixture fixture = CreateSkillFixture(uses: 2);
            CardView card = CreateCard(fixture);
            SkillCardState selectedSkill = null;
            EventInfo selectedEvent = typeof(CardView).GetEvent(
                "Selected",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(selectedEvent, Is.Not.Null, "CardView must expose a public Selected event.");
            Assert.That(selectedEvent.EventHandlerType, Is.EqualTo(typeof(Action<SkillCardState>)));

            Action<SkillCardState> selectedHandler = skill => selectedSkill = skill;
            selectedEvent.AddEventHandler(card, selectedHandler);

            card.GetComponent<Button>().onClick.Invoke();

            Assert.That(selectedSkill, Is.SameAs(fixture.Skill));
            Assert.That(fixture.Skill.RemainingUses, Is.EqualTo(2));
            Assert.That(fixture.Damage.Requests, Is.Empty);
        }

        [Test]
        public void CardDisplay_UsesItemDefinitionNameIconAndDescription()
        {
            SkillFixture fixture = CreateSkillFixture(uses: 2);
            Sprite itemIcon = CreateSprite("ItemIcon");
            SetObjectField(fixture.Item, "icon", itemIcon);
            SetStringField(fixture.Item, "displayName", "Item Name");
            SetStringField(fixture.Item, "description", "Item Description");

            CardView card = CreateCard(fixture);
            card.Bind(fixture.Skill);

            Assert.That(card.transform.Find("Title").GetComponent<TMP_Text>().text,
                Is.EqualTo("Item Name"));
            Assert.That(card.transform.Find("Description").GetComponent<TMP_Text>().text,
                Is.EqualTo("Item Description"));
            Assert.That(card.transform.Find("Icon").GetComponent<Image>().sprite,
                Is.SameAs(itemIcon));
            Assert.That(card.GetComponentsInChildren<UnityEngine.UI.Text>(true), Is.Empty);
        }

        [Test]
        public void ActionPanel_ShowsReleaseAndCancelWithTmpTextOnSharedCanvas()
        {
            SkillFixture fixture = CreateSkillFixture(uses: 1);
            PanelFixture panel = CreatePanel(fixture);

            ShowPanel(panel.Component, fixture.Skill);

            Assert.That(panel.Root.GetComponentInParent<Canvas>(), Is.Not.Null);
            Assert.That(panel.ReleaseLabel.text, Is.EqualTo("释放"));
            Assert.That(panel.CancelLabel.text, Is.EqualTo("取消选择"));
            Assert.That(panel.Root.GetComponentsInChildren<UnityEngine.UI.Text>(true), Is.Empty);
        }

        [Test]
        public void CancelSelection_HidesPanelWithoutActivatingSkill()
        {
            SkillFixture fixture = CreateSkillFixture(uses: 1);
            PanelFixture panel = CreatePanel(fixture);
            ShowPanel(panel.Component, fixture.Skill);

            panel.CancelButton.onClick.Invoke();

            PropertyInfo visibleProperty = panel.Component.GetType().GetProperty(
                "IsVisible",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(visibleProperty, Is.Not.Null, "CardActionPanel must expose IsVisible.");
            Assert.That((bool)visibleProperty.GetValue(panel.Component), Is.False);
            Assert.That(fixture.Skill.RemainingUses, Is.EqualTo(1));
            Assert.That(fixture.Damage.Requests, Is.Empty);
        }

        [Test]
        public void OnlyReleaseButton_CallsSkillActivationService()
        {
            SkillFixture fixture = CreateSkillFixture(uses: 1);
            PanelFixture panel = CreatePanel(fixture);
            ShowPanel(panel.Component, fixture.Skill);

            Assert.That(fixture.Skill.RemainingUses, Is.EqualTo(1));
            Assert.That(fixture.Damage.Requests, Is.Empty);

            panel.ReleaseButton.onClick.Invoke();

            Assert.That(fixture.Skill.RemainingUses, Is.Zero);
            Assert.That(fixture.Damage.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void FailedRelease_KeepsSelectionVisible()
        {
            SkillFixture fixture = CreateSkillFixture(uses: 1);
            PanelFixture panel = CreatePanel(fixture);
            ShowPanel(panel.Component, fixture.Skill);
            fixture.Skill.ConsumeUse();

            panel.ReleaseButton.onClick.Invoke();

            PropertyInfo selectedProperty = panel.Component.GetType().GetProperty(
                "SelectedCardId",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(panel.Root.activeSelf, Is.True);
            Assert.That(selectedProperty, Is.Not.Null);
            Assert.That((int)selectedProperty.GetValue(panel.Component), Is.EqualTo(fixture.Skill.CardId));
        }

        private CardView CreateCard(SkillFixture fixture)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            Assert.That(prefab, Is.Not.Null, CardPrefabPath);

            GameObject cardObject = UnityEngine.Object.Instantiate(prefab);
            _createdObjects.Add(cardObject);
            CardView card = cardObject.GetComponent<CardView>();
            card.Bind(fixture.Skill);
            card.ApplyAvailability(SkillActivationFailureReason.None);
            return card;
        }

        private PanelFixture CreatePanel(SkillFixture fixture)
        {
            Type panelType = FindType("_Project.UI.Card.CardActionPanel");
            Assert.That(panelType, Is.Not.Null,
                "Production must provide _Project.UI.Card.CardActionPanel.");

            GameObject canvasObject = new("SharedCanvas", typeof(RectTransform), typeof(Canvas));
            _createdObjects.Add(canvasObject);
            GameObject panelObject = new("CardActionPanel", typeof(RectTransform), typeof(CanvasGroup));
            _createdObjects.Add(panelObject);
            panelObject.transform.SetParent(canvasObject.transform, false);
            panelObject.SetActive(false);

            Button releaseButton = CreateButton(panelObject.transform, "ReleaseButton", "旧释放文本");
            Button cancelButton = CreateButton(panelObject.transform, "CancelButton", "旧取消文本");
            Component panel = panelObject.AddComponent(panelType);
            SetPanelReferences(panel, releaseButton, cancelButton);
            SetAssignableField(panel, fixture.Service);
            panelObject.SetActive(true);

            return new PanelFixture(
                panelObject,
                panel,
                releaseButton,
                cancelButton,
                releaseButton.GetComponentInChildren<TMP_Text>(true),
                cancelButton.GetComponentInChildren<TMP_Text>(true));
        }

        private static void ShowPanel(Component panel, SkillCardState skill)
        {
            MethodInfo showMethod = panel.GetType().GetMethod(
                "Show",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(SkillCardState), typeof(SkillActivationFailureReason) },
                null);
            Assert.That(showMethod, Is.Not.Null,
                "CardActionPanel must accept the selected card and its shared availability result.");
            showMethod.Invoke(panel, new object[] { skill, SkillActivationFailureReason.None });
        }

        private static Button CreateButton(Transform parent, string name, string initialText)
        {
            GameObject buttonObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            GameObject labelObject = new(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            labelObject.GetComponent<TMP_Text>().text = initialText;
            return buttonObject.GetComponent<Button>();
        }

        private static void SetPanelReferences(Component panel, Button releaseButton, Button cancelButton)
        {
            Type stateViewType = FindType("UIButtonStateView");
            foreach (FieldInfo field in panel.GetType().GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                string fieldName = field.Name.ToLowerInvariant();
                bool isRelease = fieldName.Contains("release") || fieldName.Contains("confirm");
                bool isCancel = fieldName.Contains("cancel") || fieldName.Contains("clear");
                if (typeof(Button).IsAssignableFrom(field.FieldType))
                {
                    if (isRelease)
                    {
                        field.SetValue(panel, releaseButton);
                    }
                    else if (isCancel)
                    {
                        field.SetValue(panel, cancelButton);
                    }
                }
                else if (typeof(TMP_Text).IsAssignableFrom(field.FieldType))
                {
                    if (isRelease)
                    {
                        field.SetValue(panel, releaseButton.GetComponentInChildren<TMP_Text>(true));
                    }
                    else if (isCancel)
                    {
                        field.SetValue(panel, cancelButton.GetComponentInChildren<TMP_Text>(true));
                    }
                }
                else if (stateViewType != null && field.FieldType == stateViewType && isRelease)
                {
                    field.SetValue(panel, releaseButton.gameObject.AddComponent(stateViewType));
                }
            }
        }

        private SkillFixture CreateSkillFixture(int uses)
        {
            SkillDefinitionSO definition = SkillDefinitionSO.CreateRuntimeDefinition("Skill", 10f);
            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Central,
                new SkillGrant(1f, uses, 0f));
            ItemDefinition item = ItemDefinition.CreateRuntime(
                "item",
                "Item",
                null,
                ItemShape.Single,
                combat,
                definition);
            ItemVM itemInstance = new(item);
            SkillHand hand = new();
            new CentralSkillLoadout(new ItemCombatDefinitionResolver(), hand).ReplaceItems(
                new[] { itemInstance });
            EnergyService energy = new();
            energy.Initialize(20f, 20f);
            _disposables.Add(energy);
            TestDamageProcessor damage = new();
            TestDamageable target = new(Vector3.forward);
            SkillActivationService service = new(
                energy,
                new TestCombatPhaseService(),
                damage,
                new TestSpatialQueryService(target),
                hand,
                new TestItemConsumer(itemInstance.InstanceId),
                new SkillExecutor(damage, new AttackInstanceIdGenerator()));

            _createdObjects.Add(definition);
            _createdObjects.Add(combat);
            _createdObjects.Add(item);
            return new SkillFixture(item, definition, hand.Skills[0], service, damage);
        }

        private Sprite CreateSprite(string name)
        {
            Texture2D texture = new(2, 2);
            texture.name = name + "Texture";
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            sprite.name = name;
            _createdObjects.Add(texture);
            _createdObjects.Add(sprite);
            return sprite;
        }

        private static void SetStringField(UnityEngine.Object target, string fieldName, string value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(fieldName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectField(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAssignableField(object target, object value)
        {
            Type valueType = value.GetType();
            foreach (FieldInfo field in target.GetType().GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType.IsAssignableFrom(valueType))
                {
                    field.SetValue(target, value);
                    return;
                }
            }

            Assert.Fail($"{target.GetType().Name} must receive {valueType.Name} through its composition boundary.");
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type exactType = assembly.GetType(fullName);
                if (exactType != null)
                {
                    return exactType;
                }
            }

            string simpleName = fullName.Substring(fullName.LastIndexOf('.') + 1);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.Name == simpleName)
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private sealed class SkillFixture
        {
            public ItemDefinition Item { get; }
            public SkillDefinitionSO Definition { get; }
            public SkillCardState Skill { get; }
            public SkillActivationService Service { get; }
            public TestDamageProcessor Damage { get; }

            public SkillFixture(
                ItemDefinition item,
                SkillDefinitionSO definition,
                SkillCardState skill,
                SkillActivationService service,
                TestDamageProcessor damage)
            {
                Item = item;
                Definition = definition;
                Skill = skill;
                Service = service;
                Damage = damage;
            }
        }

        private sealed class PanelFixture
        {
            public GameObject Root { get; }
            public Component Component { get; }
            public Button ReleaseButton { get; }
            public Button CancelButton { get; }
            public TMP_Text ReleaseLabel { get; }
            public TMP_Text CancelLabel { get; }

            public PanelFixture(
                GameObject root,
                Component component,
                Button releaseButton,
                Button cancelButton,
                TMP_Text releaseLabel,
                TMP_Text cancelLabel)
            {
                Root = root;
                Component = component;
                ReleaseButton = releaseButton;
                CancelButton = cancelButton;
                ReleaseLabel = releaseLabel;
                CancelLabel = cancelLabel;
            }
        }

        private sealed class TestCombatPhaseService : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }

        private sealed class TestItemConsumer : ICombatItemConsumptionService
        {
            private readonly int _itemInstanceId;

            public TestItemConsumer(int itemInstanceId)
            {
                _itemInstanceId = itemInstanceId;
            }

            public bool CanConsumeItemInstance(int itemInstanceId)
            {
                return itemInstanceId == _itemInstanceId;
            }

            public bool TryConsumeItemInstance(int itemInstanceId)
            {
                return CanConsumeItemInstance(itemInstanceId);
            }
        }

        private sealed class TestSpatialQueryService : ISpatialQueryService
        {
            private readonly List<IDamageable> _targets;

            public TestSpatialQueryService(IDamageable target)
            {
                _targets = new List<IDamageable> { target };
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
            public List<DamageRequest> Requests { get; } = new();

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

        private sealed class TestDamageable : IDamageable
        {
            public Vector3 Position { get; }
            public bool IsAlive => true;
            public Transform Transform => null;

            public TestDamageable(Vector3 position)
            {
                Position = position;
            }
        }
    }
}
