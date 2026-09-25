using System.IO;
using CombatSystem;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyFeedbackArchitectureTests
{
    private static string ProjectPath => Directory.GetParent(Application.dataPath).FullName;

    [Test]
    public void DamageProcessor_ExposesAppliedDamageEventSource()
    {
        Assert.IsTrue(typeof(IDamageEventSource).IsAssignableFrom(typeof(DamageRequestProcessor)));
    }

    [Test]
    public void ContactDamage_UsesDamageProcessor()
    {
        string source = ReadProjectFile(
            "Assets/_Project/Scripts/Enemy/Components/EnemyContactDamageComponent.cs");

        StringAssert.Contains("IDamageRequestProcessor", source);
        StringAssert.Contains("TryApplyDamage", source);
    }

    [Test]
    public void DeathExplosion_UsesDamageProcessor()
    {
        string source = ReadProjectFile(
            "Assets/_Project/Scripts/Enemy/Components/EnemyDeathExplosionComponent.cs");

        StringAssert.Contains("IDamageRequestProcessor", source);
        StringAssert.DoesNotContain("receiver.ReceiveDamageRequest", source);
    }

    [Test]
    public void DeathFlow_KeepsFlockEntityUntilSpawnerRelease()
    {
        string source = ReadProjectFile("Assets/_Project/Scripts/Enemy/Enemy.cs");
        int methodStart = source.IndexOf("DieFromHealth", System.StringComparison.Ordinal);
        int methodEnd = source.IndexOf("SetDamagePresentationState", methodStart, System.StringComparison.Ordinal);

        Assert.GreaterOrEqual(methodStart, 0);
        Assert.Greater(methodEnd, methodStart);
        string deathFlow = source.Substring(methodStart, methodEnd - methodStart);
        StringAssert.DoesNotContain("ReleaseAgent", deathFlow);
    }

    [Test]
    public void FlockShader_ContainsVisualStateInputs()
    {
        string source = ReadProjectFile(
            "Assets/_Project/Scripts/Flocking/ECS/EnemyFlockToon.shader");

        StringAssert.Contains("_Health01", source);
        StringAssert.Contains("_HitFlash", source);
        StringAssert.Contains("_DeathProgress", source);
        StringAssert.Contains("UNITY_DOTS_INSTANCING_START", source);
        StringAssert.Contains("Dissolve", source);
    }

    /// <summary>验证裂纹使用 UV 和 Voronoi 单元边界，而不是命中点或单个特征点距离。</summary>
    [Test]
    public void FlockShader_UsesUvVoronoiBoundary()
    {
        string source = ReadProjectFile(
            "Assets/_Project/Scripts/Flocking/ECS/EnemyFlockToon.shader");

        StringAssert.Contains("float2 uv : TEXCOORD0", source);
        StringAssert.Contains("float f1", source);
        StringAssert.Contains("float f2", source);
        StringAssert.Contains("f2 - f1", source);
        StringAssert.Contains("input.uv", source);
        StringAssert.DoesNotContain("Voronoi3D", source);
        StringAssert.DoesNotContain("input.positionOS * 2.5", source);
    }

    /// <summary>验证血量视觉唯一读取 R3 Health01，而不是由伤害事件重算。</summary>
    [Test]
    public void EnemyVisual_UsesR3HealthAsHealthSource()
    {
        string source = ReadProjectFile(
            "Assets/_Project/Scripts/Enemy/Visual/Implement/EnemyVisualSystem.cs");

        StringAssert.Contains("health.Health01", source);
        StringAssert.Contains("SetHealth01", source);
        StringAssert.DoesNotContain("damageEvent.RemainingHealth", source);
    }

    private static string ReadProjectFile(string relativePath)
    {
        string path = Path.Combine(ProjectPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(path), $"缺少项目文件: {relativePath}");
        return File.ReadAllText(path);
    }
}
