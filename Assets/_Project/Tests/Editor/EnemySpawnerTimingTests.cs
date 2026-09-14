using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证敌人死亡表现和对象池回收使用同一套游戏时间基准。
    /// </summary>
    public sealed class EnemySpawnerTimingTests
    {
        [Test]
        public void DeathHighlightRelease_UsesGameTime()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Enemy/Spawning/EnemySpawner.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(
                source,
                Does.Contain("Time.time + enemy.DeathHighlightDuration"));
            Assert.That(
                source,
                Does.Contain("float currentTime = Time.time"));
            Assert.That(
                source,
                Does.Not.Contain("Time.unscaledTime + enemy.DeathHighlightDuration"));
            Assert.That(
                source,
                Does.Not.Contain("float currentTime = Time.unscaledTime"));
        }
    }
}
