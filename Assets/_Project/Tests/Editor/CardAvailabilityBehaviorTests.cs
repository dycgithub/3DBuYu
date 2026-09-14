using CombatSystem;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using _Project.UI.Card;

namespace _Project.Tests.Editor
{
    /// <summary>验证卡面与操作面板消费共享的可用性结果，不自行查询目标。</summary>
    public sealed class CardAvailabilityBehaviorTests
    {
        /// <summary>同一张卡可以由外部结果切换禁用和可用状态，期间保留绑定身份。</summary>
        [Test]
        public void CardUsesSharedAvailability()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Card/CardView.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            var definition = SkillDefinitionSO.CreateRuntimeDefinition("view", 1f);
            var item = ItemDefinition.CreateRuntime("item", "物品", null, ItemShape.Single);
            try
            {
                var card = SkillCardState.Create(7, item, new SkillGrant(1f, 2, 0f), new[] { definition });
                CardView view = instance.GetComponent<CardView>();
                view.Bind(card);
                view.ApplyAvailability(SkillActivationFailureReason.InsufficientEnergy);
                Assert.That(instance.GetComponent<Button>().interactable, Is.False);
                view.ApplyAvailability(SkillActivationFailureReason.None);
                Assert.That(instance.GetComponent<Button>().interactable, Is.True);
                Assert.That(view.Skill, Is.SameAs(card));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(definition);
            }
        }

        /// <summary>选中卡被移除时，共享面板清空选择并隐藏。</summary>
        [Test]
        public void MissingCardClearsSelection()
        {
            var root = new GameObject("PanelSelectionTest", typeof(RectTransform));
            var definition = SkillDefinitionSO.CreateRuntimeDefinition("selection", 1f);
            try
            {
                var card = SkillCardState.Create(7, null, null, new[] { definition });
                CardActionPanel panel = root.AddComponent<CardActionPanel>();
                panel.Show(card, SkillActivationFailureReason.None);
                Assert.That(panel.SelectedCardId, Is.EqualTo(card.CardId));
                panel.ApplyAvailability(SkillActivationFailureReason.SkillNotFound);
                Assert.That(panel.IsVisible, Is.False);
                Assert.That(panel.SelectedCardId, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        /// <summary>冷却、次数和执行失败都向玩家显示明确的不可用原因。</summary>
        [TestCase(SkillActivationFailureReason.CooldownActive, "冷却中")]
        [TestCase(SkillActivationFailureReason.UsesExhausted, "次数耗尽")]
        [TestCase(SkillActivationFailureReason.ActionFailed, "技能执行失败")]
        [TestCase(SkillActivationFailureReason.InvalidTarget, "目标配置无效")]
        [TestCase(SkillActivationFailureReason.NoValidTarget, "没有可用目标")]
        public void CardDisplaysAvailabilityReason(
            SkillActivationFailureReason reason,
            string expectedMessage)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Card/CardView.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            SkillDefinitionSO definition = SkillDefinitionSO.CreateRuntimeDefinition("view", 1f);
            ItemDefinition item = ItemDefinition.CreateRuntime("item", "物品", null, ItemShape.Single);
            try
            {
                SkillCardState card = SkillCardState.Create(
                    7,
                    item,
                    new SkillGrant(1f, 2, 0f),
                    new[] { definition });
                CardView view = instance.GetComponent<CardView>();
                view.Bind(card);
                view.ApplyAvailability(reason);

                TMP_Text description = FindDescription(instance);
                Assert.That(description.text, Does.Contain(expectedMessage));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(definition);
            }
        }

        private static TMP_Text FindDescription(GameObject instance)
        {
            TMP_Text[] texts = instance.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text.name == "Description")
                {
                    return text;
                }
            }

            Assert.Fail("CardView prefab must contain a Description text.");
            return null;
        }
    }
}
