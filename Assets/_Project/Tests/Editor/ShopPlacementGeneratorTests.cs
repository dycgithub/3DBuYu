using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace InventorySystem.Shop
{
    /// <summary>
    /// 验证商店生成序列的短期重复限制，同时确认面积筛选仍按原规则工作。
    /// </summary>
    public sealed class ShopPlacementGeneratorTests
    {
        private readonly List<ItemDefinition> _definitions = new();

        [SetUp]
        public void SetUp()
        {
            Random.InitState(12345);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < _definitions.Count; index++)
            {
                Object.DestroyImmediate(_definitions[index]);
            }

            _definitions.Clear();
        }

        [Test]
        public void CooldownBlocksRecentItems()
        {
            IReadOnlyList<ItemDefinition> definitions = CreateDefinitions(4);
            IReadOnlyList<ShopPlacementGenerator.Placement> placements =
                Generate(definitions, 8, 1, 32);

            Assert.That(placements, Has.Count.EqualTo(8));
            AssertCooldown(placements, 3);
        }

        [Test]
        public void CooldownAllowsFourthItem()
        {
            IReadOnlyList<ItemDefinition> definitions = CreateDefinitions(4);
            IReadOnlyList<ShopPlacementGenerator.Placement> placements =
                Generate(definitions, 5, 1, 32);

            Assert.That(placements, Has.Count.EqualTo(5));
            Assert.That(placements[4].Definition, Is.SameAs(placements[0].Definition));
        }

        [Test]
        public void CooldownResetsPerRefresh()
        {
            ItemDefinition definition = CreateDefinition("item-001");
            ShopPlacementGenerator generator = new();

            IReadOnlyList<ShopPlacementGenerator.Placement> firstRefresh = generator.Generate(
                new[] { definition },
                null,
                1,
                1,
                _ => 1f,
                4);
            IReadOnlyList<ShopPlacementGenerator.Placement> secondRefresh = generator.Generate(
                new[] { definition },
                null,
                1,
                1,
                _ => 1f,
                4);

            Assert.That(firstRefresh, Has.Count.EqualTo(1));
            Assert.That(secondRefresh, Has.Count.EqualTo(1));
        }

        [Test]
        public void CooldownStopsWhenOnlyItem()
        {
            IReadOnlyList<ItemDefinition> definitions = new[]
            {
                CreateDefinition("item-001")
            };

            IReadOnlyList<ShopPlacementGenerator.Placement> placements =
                Generate(definitions, 4, 1, 16);

            Assert.That(placements, Has.Count.EqualTo(1));
        }

        [Test]
        public void AreaFilterStillWorks()
        {
            ItemDefinition single = CreateDefinition("item-single", ItemShape.Single);
            ItemDefinition vertical = CreateDefinition("item-vertical", ItemShape.Vertical2);

            IReadOnlyList<ShopPlacementGenerator.Placement> placements = new ShopPlacementGenerator().Generate(
                new[] { single, vertical },
                null,
                2,
                4,
                area => area == 2 ? 1f : 0f,
                16);

            Assert.That(placements, Is.Not.Empty);
            for (int index = 0; index < placements.Count; index++)
            {
                Assert.That(placements[index].Definition, Is.SameAs(vertical));
            }
        }

        private IReadOnlyList<ShopPlacementGenerator.Placement> Generate(
            IReadOnlyList<ItemDefinition> definitions,
            int width,
            int height,
            int maxAttempts)
        {
            return new ShopPlacementGenerator().Generate(
                definitions,
                null,
                width,
                height,
                _ => 1f,
                maxAttempts);
        }

        private ItemDefinition CreateDefinition(
            string id,
            ItemShape shape = ItemShape.Single)
        {
            ItemDefinition definition = ItemDefinition.CreateRuntime(
                id,
                id,
                null,
                shape);
            _definitions.Add(definition);
            return definition;
        }

        private IReadOnlyList<ItemDefinition> CreateDefinitions(int count)
        {
            var definitions = new List<ItemDefinition>(count);
            for (int index = 0; index < count; index++)
            {
                definitions.Add(CreateDefinition($"item-{index:000}"));
            }

            return definitions;
        }

        private static void AssertCooldown(
            IReadOnlyList<ShopPlacementGenerator.Placement> placements,
            int cooldownCount)
        {
            for (int currentIndex = 0; currentIndex < placements.Count; currentIndex++)
            {
                for (int previousIndex = 1; previousIndex <= cooldownCount; previousIndex++)
                {
                    int comparedIndex = currentIndex - previousIndex;
                    if (comparedIndex < 0)
                    {
                        break;
                    }

                    Assert.That(
                        placements[currentIndex].Definition,
                        Is.Not.SameAs(placements[comparedIndex].Definition),
                        $"生成位置 {currentIndex} 与 {comparedIndex} 重复");
                }
            }
        }
    }
}
