using CombatSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>验证一次性中央技能被消费时，其限时发射器效果不会提前清除。</summary>
public sealed class HunterProtocolConsumptionTests
{
    private GameObject _gridObject;
    private GridView _grid;
    private TransmitterGlobalEffectService _globalEffects;
    private CombatLoadoutInventoryBinder _binder;
    private ItemDefinition _definition;
    private ItemVM _item;

    /// <summary>释放绑定器和测试网格，并清除测试全局效果。</summary>
    [TearDown]
    public void TearDown()
    {
        _binder?.Dispose();

        if (_gridObject != null)
        {
            Object.DestroyImmediate(_gridObject);
        }

        if (_definition != null)
        {
            Object.DestroyImmediate(_definition);
        }
    }

    /// <summary>单次技能消费来源物品后保留效果，后续库存变更仍执行常规清理。</summary>
    [Test]
    public void ConsumingSkillItemPreservesEffectUntilNextLoadoutChange()
    {
        _definition = ItemDefinition.CreateRuntime(
            "023",
            "Hunter Protocol Test",
            null,
            ItemShape.Single);

        _gridObject = new GameObject("HunterProtocolConsumptionTestGrid");
        _gridObject.SetActive(false);
        _grid = _gridObject.AddComponent<GridView>();
        var serializedGrid = new SerializedObject(_grid);
        serializedGrid.FindProperty("gridType").enumValueIndex = (int)GridType.CentralBackpack;
        serializedGrid.ApplyModifiedProperties();
        _grid.EnsureGridVM();
        _item = new ItemVM(
            _definition,
            basePoints: new[] { Vector2Int.zero });
        Assert.That(_grid.GridVM.Place(_item, 0, 0), Is.True);

        var resolver = new ItemCombatDefinitionResolver();
        var skillLoadout = new CentralSkillLoadout(resolver, new SkillHand());
        var transmitterLoadout = new TransmitterAttackLoadout(resolver);
        _globalEffects = new TransmitterGlobalEffectService();
        _binder = new CombatLoadoutInventoryBinder(
            skillLoadout,
            transmitterLoadout,
            null,
            _globalEffects);

        _gridObject.SetActive(true);
        _binder.Start();
        _globalEffects.SetAttackDamageMultiplier(2f, _item.InstanceId, 5f);

        Assert.That(_binder.TryConsumeItemInstance(_item.InstanceId), Is.True);
        Assert.That(_globalEffects.AttackDamageMultiplier, Is.EqualTo(2f));

        _grid.NotifyItemsChanged();

        Assert.That(_globalEffects.AttackDamageMultiplier, Is.EqualTo(1f));
    }
}
