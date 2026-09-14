using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 防止死亡爆炸组件重复挂载后重复响应同一次死亡事件。
    /// </summary>
    public sealed class EnemyComponentUniquenessTests
    {
        [Test]
        public void DeathExplosionComponent_AllowsOnlyOneInstancePerEnemy()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Enemy/Components/EnemyDeathExplosionComponent.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Contain("[DisallowMultipleComponent]"));
        }
    }
}
