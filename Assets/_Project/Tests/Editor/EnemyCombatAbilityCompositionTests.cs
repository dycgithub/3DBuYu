using System.IO;
using EnemySystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定当前敌人不依赖战斗技能，且技能组件不由敌人工厂或敌人 Prefab 组合。
    /// </summary>
    public sealed class EnemyCombatAbilityCompositionTests
    {
        [Test]
        public void EnemyFactory_DoesNotDependOnCombatAbility()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Enemy/Spawning/EnemyFactory.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Not.Contain("EnemyCombatAbility"));
            Assert.That(source, Does.Not.Contain("IObjectResolver"));
            Assert.That(source, Does.Not.Contain("_resolver"));
        }

        [TestCase("EnemyNormal.prefab")]
        [TestCase("EnemyFast.prefab")]
        [TestCase("EnemyTank.prefab")]
        public void EnemyPrefab_DoesNotContainCombatAbility(string fileName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/_Project/Prefabs/Enemy/{fileName}");

            Assert.That(prefab, Is.Not.Null, fileName);
            Assert.That(prefab.GetComponent<EnemyCombatAbility>(), Is.Null, fileName);
        }
    }
}
