using CombatSystem;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using _Project.UI.Card;

namespace _Project.Tests.Editor
{
    /// <summary>验证卡牌显示物品信息、能量、次数和冷却。</summary>
    public sealed class ItemSkillCardPresentationTests
    {
        private const string CardPrefabPath = "Assets/_Project/Prefabs/UI/Card/CardView.prefab";

        [Test]
        public void CardPrefabContainsSkillStatusTexts()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            Assert.That(prefab, Is.Not.Null, CardPrefabPath);
            Assert.That(FindText(prefab, "Energy"), Is.Not.Null);
            Assert.That(FindText(prefab, "Uses"), Is.Not.Null);
            Assert.That(FindText(prefab, "Cooldown"), Is.Not.Null);
        }

        [Test]
        public void BindDisplaysEnergyUsesAndCooldown()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            ItemDefinition item = ItemDefinition.CreateRuntime(
                "001",
                "解锁发射器",
                null,
                ItemShape.Plus5);
            SkillDefinitionSO skill = SkillDefinitionSO.CreateRuntimeDefinition("unlock", 0f);

            try
            {
                SkillCardState card = SkillCardState.Create(
                    1,
                    item,
                    new SkillGrant(3f, 2, 4f),
                    new[] { skill });
                instance.GetComponent<CardView>().Bind(card);

                Assert.That(FindText(instance, "Energy").text, Does.Contain("3"));
                Assert.That(FindText(instance, "Uses").text, Does.Contain("2"));
                Assert.That(FindText(instance, "Cooldown").text, Does.Contain("4"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(skill);
            }
        }

        private static TMP_Text FindText(GameObject root, string name)
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text.name == name)
                {
                    return text;
                }
            }

            return null;
        }
    }
}
