using System.Linq;
using CombatSystem;
using NUnit.Framework;
using UnityEditor;

/// <summary>验证新增道具配置、图标辨识度和灼烧持续伤害规则。</summary>
public sealed class ItemMechanicCoverageTests
{
    private const string CatalogPath = "Assets/_Project/SO/Item/ItemCatalog.asset";

    [Test]
    public void Catalog_HasDistinctIcons()
    {
        ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);

        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.Items, Has.Count.EqualTo(25));
        Assert.That(catalog.Items.Select(item => item.Id).Distinct().Count(), Is.EqualTo(25));
        Assert.That(catalog.Items.All(item => item.Icon != null), Is.True);
        Assert.That(catalog.Items.Select(item => item.Icon).Distinct().Count(), Is.EqualTo(25));
    }

    [Test]
    public void NewItems_HaveExpectedScopes()
    {
        ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);

        for (int id = 16; id <= 21; id++)
        {
            AssertItemScope(catalog, id, CombatScope.Transmitter);
        }

        for (int id = 22; id <= 23; id++)
        {
            AssertItemScope(catalog, id, CombatScope.Central);
        }

        for (int id = 24; id <= 25; id++)
        {
            AssertItemScope(catalog, id, CombatScope.CentralAndTransmitter);
        }
    }

    [Test]
    public void TransmitterItems_HaveConfiguredMechanics()
    {
        ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        ItemDefinition burn = catalog.GetById("016");
        ItemDefinition weaken = catalog.GetById("017");
        ItemDefinition execution = catalog.GetById("018");
        ItemDefinition combo = catalog.GetById("019");
        ItemDefinition ricochet = catalog.GetById("020");
        ItemDefinition refund = catalog.GetById("021");

        ApplyDamageOverTimeEffectDefinitionSO burnEffect =
            burn.CombatDefinition.TransmitterProjectileProfile.OnHitEffectDefinitions[0]
                as ApplyDamageOverTimeEffectDefinitionSO;
        Assert.That(burnEffect, Is.Not.Null);
        Assert.That(burnEffect.DamageRatio, Is.EqualTo(0.15f));
        Assert.That(burnEffect.DurationSeconds, Is.EqualTo(3f));

        ApplyBuffEffectDefinitionSO weakenEffect =
            weaken.CombatDefinition.TransmitterProjectileProfile.OnHitEffectDefinitions[0]
                as ApplyBuffEffectDefinitionSO;
        Assert.That(weakenEffect, Is.Not.Null);
        Assert.That(weakenEffect.BuffDefinition.ModifierType, Is.EqualTo(BuffModifierType.AttackDamage));
        Assert.That(weakenEffect.BuffDefinition.ModifierValue, Is.EqualTo(0.75f));

        ProjectileProfileSO executionProfile = execution.CombatDefinition.TransmitterProjectileProfile;
        Assert.That(executionProfile.ExecutionHealthThreshold, Is.EqualTo(0.25f));
        Assert.That(executionProfile.ExecutionDamageMultiplier, Is.EqualTo(1.5f));

        ProjectileProfileSO comboProfile = combo.CombatDefinition.TransmitterProjectileProfile;
        Assert.That(comboProfile.ComboDamageBonusPerHit, Is.EqualTo(0.1f));
        Assert.That(comboProfile.ComboMaximumStacks, Is.EqualTo(3));
        Assert.That(comboProfile.ComboResetSeconds, Is.EqualTo(2f));

        ProjectileProfileSO ricochetProfile = ricochet.CombatDefinition.TransmitterProjectileProfile;
        Assert.That(ricochetProfile.RicochetCount, Is.EqualTo(2));
        Assert.That(ricochetProfile.RicochetSearchRadius, Is.EqualTo(15f));
        Assert.That(ricochetProfile.RicochetDamageMultiplier, Is.EqualTo(0.7f));

        Assert.That(
            refund.CombatDefinition.TransmitterProjectileProfile.KillEnergyRefundRatio,
            Is.EqualTo(0.2f));
    }

    [Test]
    public void CentralItems_HaveConfiguredActions()
    {
        ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        CentralSkillActionDefinitionSO freeze = catalog.GetById("022")
            .SkillDefinitions[0].ActionDefinition as CentralSkillActionDefinitionSO;
        CentralSkillActionDefinitionSO focus = catalog.GetById("023")
            .SkillDefinitions[0].ActionDefinition as CentralSkillActionDefinitionSO;

        Assert.That(freeze, Is.Not.Null);
        Assert.That(freeze.ActionKind, Is.EqualTo(CentralSkillActionKind.FreezeEnemies));
        Assert.That(focus, Is.Not.Null);
        Assert.That(focus.ActionKind, Is.EqualTo(CentralSkillActionKind.FocusHighestHealth));
        Assert.That(focus.Value, Is.EqualTo(5f));
    }

    [Test]
    public void SharedItems_HaveConfiguredEffects()
    {
        ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        ItemCombatDefinitionSO marked = catalog.GetById("024").CombatDefinition;
        ItemCombatDefinitionSO efficient = catalog.GetById("025").CombatDefinition;
        CentralSkillActionDefinitionSO markAll = catalog.GetById("024")
            .SkillDefinitions[0].ActionDefinition as CentralSkillActionDefinitionSO;

        Assert.That(marked.TransmitterProjectileProfile.OnHitEffectDefinitions, Has.Count.EqualTo(1));
        Assert.That(markAll, Is.Not.Null);
        Assert.That(markAll.ActionKind, Is.EqualTo(CentralSkillActionKind.ApplyBuffToEnemies));
        Assert.That(efficient.TransmitterEnergyCostMultiplier, Is.EqualTo(0.8f));
        Assert.That(efficient.CentralEnergyCostMultiplier, Is.EqualTo(0.8f));
    }

    [Test]
    public void Burn_StopsAtDurationBoundary()
    {
        BuffManner manner = new();
        RecordingBuffTarget target = new();
        manner.RegisterTarget(target);

        try
        {
            manner.ApplyDamageOverTime(target, 100f, 0.25f, 7);

            manner.Process(0.2f);
            Assert.That(target.TotalDamage, Is.EqualTo(20f).Within(0.001f));
            Assert.That(target.LastDamageType, Is.EqualTo(DamageType.Fire));

            manner.Process(0.2f);
            Assert.That(target.TotalDamage, Is.EqualTo(25f).Within(0.001f));

            manner.Process(0.1f);
            Assert.That(target.TotalDamage, Is.EqualTo(25f).Within(0.001f));
        }
        finally
        {
            manner.Dispose();
        }
    }

    [Test]
    public void Burn_RefreshesOldestAtThreeStacks()
    {
        BuffManner manner = new();
        RecordingBuffTarget target = new();
        manner.RegisterTarget(target);

        try
        {
            manner.ApplyDamageOverTime(target, 10f, 1f, 7);
            manner.ApplyDamageOverTime(target, 10f, 1f, 7);
            manner.ApplyDamageOverTime(target, 10f, 1f, 7);
            manner.Process(0.1f);
            manner.ApplyDamageOverTime(target, 20f, 1f, 7);
            manner.Process(0.1f);

            Assert.That(target.TotalDamage, Is.EqualTo(7f).Within(0.001f));
        }
        finally
        {
            manner.Dispose();
        }
    }

    private static void AssertItemScope(ItemCatalog catalog, int id, CombatScope expectedScope)
    {
        ItemDefinition item = catalog.GetById(id.ToString("000"));

        Assert.That(item, Is.Not.Null, id.ToString());
        Assert.That(item.CombatDefinition, Is.Not.Null, id.ToString());
        Assert.That(item.CombatDefinition.CombatScope, Is.EqualTo(expectedScope), id.ToString());
        Assert.That(string.IsNullOrWhiteSpace(item.DisplayName), Is.False, id.ToString());
        Assert.That(string.IsNullOrWhiteSpace(item.Description), Is.False, id.ToString());
    }

    private sealed class RecordingBuffTarget : IBuffTarget, IDamageRequestReceiver
    {
        public float TotalDamage { get; private set; }
        public DamageType LastDamageType { get; private set; }

        public bool IsActive => true;
        public bool IsAlive => true;
        public bool CanReceiveHarmfulEffects => true;

        public void OnBuffModifiersChanged(BuffModifierType modifierType)
        {
        }

        public DamageResult ReceiveDamageRequest(in DamageRequest request)
        {
            TotalDamage += request.DamageAmount;
            LastDamageType = request.DamageType;
            return default;
        }
    }
}
