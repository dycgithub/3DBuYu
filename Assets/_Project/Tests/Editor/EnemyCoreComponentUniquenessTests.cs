using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 确保敌人组合根和其核心协作组件不会被重复挂载。
    /// </summary>
    public sealed class EnemyCoreComponentUniquenessTests
    {
        [TestCase("_Project/Scripts/Enemy/Enemy.cs")]
        [TestCase("_Project/Scripts/Flocking/ECS/EnemyFlockBridge.cs")]
        [TestCase("_Project/Scripts/CombatSystem/Buff/Application/BuffController.cs")]
        public void CoreComponent_RejectsDuplicateMounting(string relativePath)
        {
            string sourcePath = Path.Combine(Application.dataPath, relativePath);
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Contain("[DisallowMultipleComponent]"));
        }
    }
}
