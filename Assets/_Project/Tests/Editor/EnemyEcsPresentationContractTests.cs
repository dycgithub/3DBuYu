using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定 ECS 敌人的受击和死亡颜色必须通过实体材质属性传递，而不是只修改隐藏的源 Renderer。
    /// </summary>
    public sealed class EnemyEcsPresentationContractTests
    {
        [Test]
        public void EcsPresentation_UsesPerEntityBaseColorOverride()
        {
            string scriptsPath = Path.Combine(Application.dataPath, "_Project/Scripts");
            string bridgeSource = File.ReadAllText(
                Path.Combine(scriptsPath, "Flocking/ECS/EnemyFlockBridge.cs"));
            string runtimeSource = File.ReadAllText(
                Path.Combine(scriptsPath, "Flocking/ECS/EnemyFlockRuntimeService.cs"));
            string presentationSource = File.ReadAllText(
                Path.Combine(scriptsPath, "Enemy/Components/EnemyDamagePresentationComponent.cs"));

            Assert.That(runtimeSource, Does.Contain("URPMaterialPropertyBaseColor"));
            Assert.That(runtimeSource, Does.Contain("SetAgentPresentationColor"));
            Assert.That(runtimeSource, Does.Contain("ResetAgentPresentationColor"));
            Assert.That(bridgeSource, Does.Contain("SetAgentPresentationColor"));
            Assert.That(bridgeSource, Does.Contain("ResetAgentPresentationColor"));
            Assert.That(presentationSource, Does.Contain("SetPresentationColor"));
            Assert.That(presentationSource, Does.Contain("ResetPresentationColor"));
        }
    }
}
