using System.Reflection;
using CombatSystem;
using NUnit.Framework;
using UnityEngine;

/// <summary>验证战斗系统基础配置和背包实例身份契约。</summary>
public sealed class CombatSystemContractTests
{
    [Test]
    public void ItemInstanceId_IsPreservedByInventorySnapshot()
    {
        ItemDefinition definition = ItemDefinition.CreateRuntime(
            "test-item", "测试物品", null, ItemShape.Single);
        ItemVM source = new ItemVM(definition);
        InventoryItemSnapshot snapshot = new InventoryItemSnapshot(source);

        ItemVM restored = snapshot.CreateItem();

        Assert.That(restored.InstanceId, Is.EqualTo(source.InstanceId));
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void UnconfiguredItemDoesNotAddImplicitDamage()
    {
        ItemDefinition definition = ItemDefinition.CreateRuntime(
            "test-item", "测试物品", null, ItemShape.Single);
        ItemVM item = new ItemVM(definition);
        var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
        loadout.ReplaceItems("port-0", new[] { item });
        var buildService = new TransmitterAttackBuildService(loadout);
        ProjectileProfileSO profile = ProjectileProfileSO.CreateRuntimeProfile("test-profile");
        var input = new TransmitterAttackInput(
            1, 0, profile, Vector3.zero, Vector3.forward,
            10f, 50f, 1f, 1, 0, 0f, 1f, "port-0");

        TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);
        var builder = new AttackDataBuilder();

        Assert.That(builder.TryBuildAttackData(in input, in build, out AttackData attack), Is.True);
        Assert.That(attack.DamageAmount, Is.EqualTo(10f));

        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void GridView_AssignsPortIdWithoutUsingObjectName()
    {
        var gridObject = new GameObject("任意名称");
        try
        {
            GridView grid = gridObject.AddComponent<GridView>();
            FieldInfo gridTypeField = typeof(GridView).GetField(
                "gridType",
                BindingFlags.Instance | BindingFlags.NonPublic);
            gridTypeField.SetValue(grid, GridType.TransmitterBackpack);

            Assert.That(grid.AssignPortId("port-6"), Is.True);
            Assert.That(grid.PortId, Is.EqualTo("port-6"));
        }
        finally
        {
            Object.DestroyImmediate(gridObject);
        }
    }
}
