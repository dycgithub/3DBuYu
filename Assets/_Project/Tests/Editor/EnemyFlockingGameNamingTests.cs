using System;
using System.Reflection;
using EnemySystem;
using NUnit.Framework;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证敌人、群游和游戏配置的公开命名约定。
    /// </summary>
    public sealed class EnemyFlockingGameNamingTests
    {
        [Test]
        public void EnemyAndWaveScriptableObjectTypes_UseTheRequiredSuffix()
        {
            string[] typeNames =
            {
                "EnemySystem.EnemyAttributesSO",
                "EnemySystem.PopulationConfigSO",
                "EnemySystem.PopulationDataSO",
            };

            foreach (string typeName in typeNames)
            {
                Type type = Type.GetType(typeName + ", Assembly-CSharp");
                Assert.That(type, Is.Not.Null, typeName);
                Assert.That(type.Name, Does.EndWith("SO"), typeName);
            }
        }

        [Test]
    public void EnemyInitialization_UsesSpawnTerminology()
        {
            MethodInfo method = typeof(Enemy).GetMethod(
                "InitializeForSpawn",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(method, Is.Not.Null);
        }
    }
}
