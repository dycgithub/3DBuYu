using System.Collections.Generic;
using CombatSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>验证独立道具视觉会覆盖外观，但不替换发射器攻击机制。</summary>
public sealed class TransmitterAttackLoadoutTests
{
    private const string PortId = "ProjectileVisualTestPort";

    private readonly List<Object> _createdObjects = new();
    private TransmitterAttackLoadout _loadout;

    /// <summary>创建本地装备汇总器。</summary>
    [SetUp]
    public void SetUp()
    {
        _loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
    }

    /// <summary>释放合并配置与测试创建的所有资产。</summary>
    [TearDown]
    public void TearDown()
    {
        _loadout?.ClearAllItems();

        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
            {
                Object.DestroyImmediate(_createdObjects[index]);
            }
        }

        _createdObjects.Clear();
    }

    /// <summary>视觉单独覆盖时继承发射器数值和轨迹。</summary>
    [Test]
    public void VisualOnlyUsesInputProfile()
    {
        ProjectileVisualDefinitionSO itemVisual = CreateVisual(Color.magenta, true);
        ItemVM item = CreateItem("visual-only", null, itemVisual);
        _loadout.ReplaceItems(PortId, new[] { item });

        ProjectileTrajectoryDefinitionSO trajectory = CreateObject<ProjectileTrajectoryDefinitionSO>();
        ProjectileProfileSO inputProfile = CreateObject<ProjectileProfileSO>();
        inputProfile.EnergyCost = 13f;
        inputProfile.SpeedUnitsPerSecond = 37f;
        inputProfile.MaximumTravelDistance = 81f;
        inputProfile.CollisionRadius = 0.27f;
        inputProfile.ScatterAngleDegrees = 19f;
        inputProfile.RandomSpeedVariance = 0.2f;
        inputProfile.ProjectileSpeedMultiplier = 1.4f;
        inputProfile.TrajectoryDefinition = trajectory;
        inputProfile.OnHitEffectDefinitions = new List<ProjectileEffectDefinitionSO>();

        var input = new TransmitterAttackInput(
            1,
            0,
            inputProfile,
            Vector3.zero,
            Vector3.forward,
            10f,
            60f,
            2f,
            1,
            0,
            0f,
            2f,
            PortId);
        var buildService = new TransmitterAttackBuildService(_loadout);

        TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);
        ProjectileProfileSO combined = build.ProjectileProfile;

        Assert.That(combined, Is.Not.SameAs(inputProfile));
        Assert.That(combined.VisualDefinition, Is.SameAs(itemVisual));
        Assert.That(combined.TrajectoryDefinition, Is.SameAs(trajectory));
        Assert.That(combined.EnergyCost, Is.EqualTo(13f));
        Assert.That(combined.SpeedUnitsPerSecond, Is.EqualTo(37f));
        Assert.That(combined.MaximumTravelDistance, Is.EqualTo(81f));
        Assert.That(combined.CollisionRadius, Is.EqualTo(0.27f));
        Assert.That(combined.ScatterAngleDegrees, Is.EqualTo(19f));
        Assert.That(combined.RandomSpeedVariance, Is.EqualTo(0.2f));
        Assert.That(combined.ProjectileSpeedMultiplier, Is.EqualTo(1.4f));
        Assert.That(combined.OnHitEffectDefinitions, Is.SameAs(inputProfile.OnHitEffectDefinitions));
        Assert.That(build.AdditiveDamageBonus, Is.Zero);
        Assert.That(build.DamageMultiplier, Is.EqualTo(1f));
    }

    /// <summary>没有发射器基础配置时，视觉不能独自生成攻击配置。</summary>
    [Test]
    public void VisualRequiresInputProfile()
    {
        ProjectileVisualDefinitionSO itemVisual = CreateVisual(Color.magenta, true);
        ItemVM item = CreateItem("visual-only", null, itemVisual);
        _loadout.ReplaceItems(PortId, new[] { item });

        var input = new TransmitterAttackInput(
            1,
            0,
            null,
            Vector3.zero,
            Vector3.forward,
            10f,
            60f,
            2f,
            1,
            0,
            0f,
            2f,
            PortId);
        var buildService = new TransmitterAttackBuildService(_loadout);

        TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);

        Assert.That(build.ProjectileProfile, Is.Null);
    }

    /// <summary>装备视觉按顺序覆盖，同时轨迹仍服从既有优先级。</summary>
    [Test]
    public void VisualKeepsTrajectoryPriority()
    {
        ProjectileVisualDefinitionSO boomerangVisual = CreateVisual(Color.blue, true);
        ProjectileVisualDefinitionSO homingVisual = CreateVisual(Color.green, true);
        ProjectileVisualDefinitionSO laserVisual = CreateVisual(Color.cyan, true);
        ProjectileVisualDefinitionSO lastVisual = CreateVisual(Color.magenta, true);

        ProjectileProfileSO boomerangProfile = CreateProfile(
            CreateObject<BoomerangTrajectoryDefinitionSO>(),
            boomerangVisual);
        ProjectileProfileSO homingProfile = CreateProfile(
            CreateObject<HomingTrajectoryDefinitionSO>(),
            homingVisual);
        ProjectileProfileSO laserProfile = CreateProfile(
            CreateObject<LaserTrajectoryDefinitionSO>(),
            laserVisual);

        ItemVM[] items =
        {
            CreateItem("boomerang", boomerangProfile, null),
            CreateItem("homing", homingProfile, null),
            CreateItem("laser", laserProfile, null),
            CreateItem("visual-only", null, lastVisual)
        };
        _loadout.ReplaceItems(PortId, items);

        TransmitterAttackModifiers modifiers = TransmitterAttackModifiers.Default;
        _loadout.CollectAttackModifiers(PortId, ref modifiers);

        Assert.That(
            modifiers.ProjectileProfileOverride.TrajectoryDefinition,
            Is.SameAs(boomerangProfile.TrajectoryDefinition));
        Assert.That(modifiers.ProjectileProfileOverride.VisualDefinition, Is.SameAs(lastVisual));
    }

    /// <summary>创建独立视觉定义。</summary>
    private ProjectileVisualDefinitionSO CreateVisual(Color tint, bool overridesEmitter)
    {
        ProjectileVisualDefinitionSO visual = CreateObject<ProjectileVisualDefinitionSO>();
        visual.TintColor = tint;
        visual.OverridesEmitterVisual = overridesEmitter;
        return visual;
    }

    /// <summary>创建绑定轨迹与视觉的投射物配置。</summary>
    private ProjectileProfileSO CreateProfile(
        TrajectoryDefinitionSO trajectory,
        ProjectileVisualDefinitionSO visual)
    {
        ProjectileProfileSO profile = CreateObject<ProjectileProfileSO>();
        profile.TrajectoryDefinition = trajectory;
        profile.VisualDefinition = visual;
        return profile;
    }

    /// <summary>创建配置了可选投射物视觉的发射器道具。</summary>
    private ItemVM CreateItem(
        string itemId,
        ProjectileProfileSO profile,
        ProjectileVisualDefinitionSO visual)
    {
        ItemCombatDefinitionSO combat = CreateObject<ItemCombatDefinitionSO>();
        SetReference(combat, "transmitterProjectileProfile", profile);
        SetReference(combat, "transmitterProjectileVisualDefinition", visual);

        ItemDefinition definition = ItemDefinition.CreateRuntime(
            itemId,
            itemId,
            null,
            ItemShape.Single,
            combat);
        _createdObjects.Add(definition);
        return new ItemVM(definition, basePoints: new[] { Vector2Int.zero });
    }

    /// <summary>将私有序列化引用写入测试中的运行时配置对象。</summary>
    private static void SetReference(
        ScriptableObject target,
        string propertyName,
        Object value)
    {
        var serializedTarget = new SerializedObject(target);
        SerializedProperty property = serializedTarget.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, propertyName);
        property.objectReferenceValue = value;
        serializedTarget.ApplyModifiedProperties();
    }

    /// <summary>创建并登记测试所需的临时 ScriptableObject。</summary>
    private T CreateObject<T>() where T : ScriptableObject
    {
        T instance = ScriptableObject.CreateInstance<T>();
        _createdObjects.Add(instance);
        return instance;
    }
}
