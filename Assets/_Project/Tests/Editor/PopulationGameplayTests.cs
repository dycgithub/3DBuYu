using System;
using CombatSystem;
using EnemySystem;
using GameSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>锁定数量补充、轨道和装备状态的新玩法契约。</summary>
    public sealed class PopulationGameplayTests
    {
        [TestCase(DifficultyLevel.Easy, 500, 0, 0)]
        [TestCase(DifficultyLevel.Normal, 800, 1, 0)]
        [TestCase(DifficultyLevel.Advanced, 1000, 1, 0)]
        [TestCase(DifficultyLevel.Expert, 1200, 10, 0)]
        [TestCase(DifficultyLevel.Nightmare, 1500, 100, 1)]
        [TestCase(DifficultyLevel.Extreme, 1800, 200, 3)]
        [TestCase(DifficultyLevel.Impossible, 1900, 95, 5)]
        public void DifficultyHasExactQuotas(DifficultyLevel level, int normal, int elite, int boss)
        {
            EnemyQuota quota = EnemyQuota.ForDifficulty(level);
            Assert.That(quota.Normal, Is.EqualTo(normal));
            Assert.That(quota.Elite, Is.EqualTo(elite));
            Assert.That(quota.Boss, Is.EqualTo(boss));
        }

        [Test]
        public void InitialPopulationCompletesAtFiveSeconds()
        {
            PopulationHarness harness = new(DifficultyLevel.Impossible);
            harness.Tick(2.5f);
            Assert.That(harness.Counts[0], Is.EqualTo(950));
            harness.Tick(2.5f);
            Assert.That(harness.Counts, Is.EqualTo(new[] { 1900, 95, 5 }));
            Assert.That(harness.Schedule.InitialCompleted, Is.True);
        }

        [Test]
        public void ThresholdIsStrictAndRefillContinuesAboveThreshold()
        {
            PopulationHarness harness = new(DifficultyLevel.Easy);
            harness.Tick(5f);
            harness.Counts[0] = 400;
            harness.Tick(1f);
            Assert.That(harness.Counts[0], Is.EqualTo(400));
            harness.Counts[0] = 399;
            harness.Tick(1f);
            Assert.That(harness.Counts[0], Is.EqualTo(449));
            harness.Tick(2f);
            Assert.That(harness.Counts[0], Is.EqualTo(500));
            Assert.That(harness.Schedule.IsRefilling, Is.False);
        }

        [Test]
        public void RefillUsesIndependentIntervalsWithoutStoredCredit()
        {
            PopulationHarness harness = new(DifficultyLevel.Impossible);
            harness.Tick(5f);
            harness.Counts[0] = 0;
            harness.Counts[1] = 0;
            harness.Counts[2] = 0;
            harness.Tick(1f);
            Assert.That(harness.Counts, Is.EqualTo(new[] { 50, 0, 0 }));
            harness.Tick(4f);
            Assert.That(harness.Counts, Is.EqualTo(new[] { 250, 1, 0 }));
            harness.Tick(55f);
            Assert.That(harness.Counts, Is.EqualTo(new[] { 1900, 12, 1 }));
            harness.Counts[0]--;
            harness.Tick(0.01f);
            Assert.That(harness.Counts[0], Is.EqualTo(1899));
        }

        [Test]
        public void FailedSpawnsDoNotConsumeInitialQuota()
        {
            PopulationHarness harness = new(DifficultyLevel.Easy) { AcceptSpawn = false };
            harness.Tick(5f);
            Assert.That(harness.Schedule.InitialCompleted, Is.False);
            harness.AcceptSpawn = true;
            harness.Tick(0.1f);
            Assert.That(harness.Counts[0], Is.EqualTo(500));
        }

        [Test]
        public void InitialDeathsDoNotTriggerExtraInitialSpawns()
        {
            PopulationHarness harness = new(DifficultyLevel.Easy);
            harness.Tick(2.5f);
            harness.Counts[0] = 0;
            harness.Tick(2.5f);
            Assert.That(harness.Counts[0], Is.EqualTo(250));
            harness.Tick(1f);
            Assert.That(harness.Counts[0], Is.EqualTo(300));
        }

        /// <summary>卸装后继续计时，并按物品实例恢复同一张卡的剩余次数和冷却。</summary>
        [Test]
        public void UnequippedSkillRetainsStateAndIsUnavailable()
        {
            SkillDefinitionSO skill = SkillDefinitionSO.CreateRuntimeDefinition("retained", 1f);
            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Central, new SkillGrant(0f, 3, 10f));
            ItemDefinition itemDefinition = ItemDefinition.CreateRuntime("retained", "保留", null, ItemShape.Single, combat, skill);
            try
            {
                ItemVM item = new(itemDefinition);
                SkillHand hand = new();
                CentralSkillLoadout loadout = new(new ItemCombatDefinitionResolver(), hand);
                loadout.ReplaceItems(new[] { item });
                SkillCardState runtime = hand.Skills[0];
                runtime.ConsumeUse();
                runtime.BeginCooldown();
                loadout.ReplaceItems(Array.Empty<ItemVM>());
                Assert.That(hand.FindByCardId(runtime.CardId), Is.Null);
                hand.AdvanceCooldowns(2f);
                loadout.ReplaceItems(new[] { item });
                Assert.That(hand.Skills[0], Is.SameAs(runtime));
                Assert.That(runtime.RemainingUses, Is.EqualTo(2));
                Assert.That(runtime.RemainingCooldownSeconds, Is.EqualTo(8f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(itemDefinition);
                UnityEngine.Object.DestroyImmediate(combat);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PureBypassesInterceptors()
        {
            GameObject instance = new("PureDamageTest");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
            try
            {
                attributes.baseHealth = 100f;
                Enemy enemy = instance.AddComponent<Enemy>();
                SerializedObject serialized = new(enemy);
                serialized.FindProperty("enemyAttributes").objectReferenceValue = attributes;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            enemy.InitializeForSpawn(1f, 1f);
                enemy.RegisterPreDamageInterceptor(Dodge);
                DamageResult result = enemy.ReceiveDamageRequest(new DamageRequest
                {
                    DamageType = DamageType.Pure, DamageAmount = 10f
                });
                Assert.That(result.AppliedDamage, Is.EqualTo(10f));
                Assert.That(enemy.CurrentHealth, Is.EqualTo(90f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(attributes);
            }
        }

        [Test]
        public void RadiusApiMovesStationaryPlayerAndKeepsDirection()
        {
            GameObject instance = new("OrbitTest");
            try
            {
                SphereWalker walker = instance.AddComponent<SphereWalker>();
                instance.transform.position = new Vector3(3f, 4f, 0f);
                walker.SetRadius(20f);
                Assert.That(instance.transform.position.magnitude, Is.EqualTo(20f).Within(0.001f));
                Assert.That(instance.transform.position.normalized, Is.EqualTo(new Vector3(0.6f, 0.8f, 0f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool Dodge(Enemy enemy, float original, ref float damage) => false;

        private sealed class PopulationHarness
        {
            public readonly int[] Counts = new int[3];
            public readonly EnemyPopulationSchedule Schedule;
            public bool AcceptSpawn = true;

            public PopulationHarness(DifficultyLevel level)
            {
                Schedule = new EnemyPopulationSchedule(EnemyQuota.ForDifficulty(level));
            }

            public void Tick(float seconds)
            {
                Schedule.Tick(seconds, rank => Counts[(int)rank], rank =>
                {
                    if (!AcceptSpawn)
                    {
                        return false;
                    }
                    Counts[(int)rank]++;
                    return true;
                });
            }
        }
    }
}
