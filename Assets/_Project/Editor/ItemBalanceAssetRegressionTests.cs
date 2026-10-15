using CombatSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Localization.Tables;

/// <summary>Locks the requested item balance values, fallback text, and localized descriptions.</summary>
public sealed class ItemBalanceAssetRegressionTests
{
    private const string Item010Path = "Assets/_Project/SO/Item/ItemDefinition_010.asset";
    private const string Item022Path = "Assets/_Project/SO/Item/ItemDefinition_022.asset";

    /// <summary>Verifies the expanded core radius leaves transmitter knockback unchanged.</summary>
    [Test]
    public void RepulsionRangeKeepsKnockback()
    {
        SkillDefinitionSO skill = Load<SkillDefinitionSO>("Assets/_Project/SO/Item/Central/SkillDefinition_010.asset");
        Assert.That(skill.TargetRadius, Is.EqualTo(10f));
        Assert.That(ReadFallbackDescription(Load<ItemDefinition>(Item010Path)),
            Is.EqualTo("中央推开附近10米的敌人；发射器命中击退1米。"));

        ProjectileProfileSO transmitterProfile = Load<ItemDefinition>(Item010Path)
            .CombatDefinition.TransmitterProjectileProfile;
        KnockbackEffectDefinitionSO knockback = Load<KnockbackEffectDefinitionSO>(
            "Assets/_Project/SO/Item/Projectile/Effect_Knockback.asset");
        Assert.That(transmitterProfile.OnHitEffectDefinitions, Does.Contain(knockback));
        Assert.That(knockback.Impulse, Is.EqualTo(1f));

        AssertTranslations(
            "item.010.description",
            "The core repels enemies within 10m. Transmitter hits knock targets back 1m.",
            "中央推开附近10米的敌人；发射器命中击退1米。");
    }

    /// <summary>Verifies movement and attack freeze buffs both last three seconds.</summary>
    [Test]
    public void FreezeBuffsShareDuration()
    {
        Assert.That(Load<BuffDefinitionSO>(
            "Assets/_Project/SO/Item/Projectile/Buff_FrozenMove_022.asset").DurationSeconds, Is.EqualTo(3f));
        Assert.That(Load<BuffDefinitionSO>(
            "Assets/_Project/SO/Item/Projectile/Buff_FrozenAttack_022.asset").DurationSeconds, Is.EqualTo(3f));
        Assert.That(ReadFallbackDescription(Load<ItemDefinition>(Item022Path)),
            Is.EqualTo("冻结全部敌人3秒，使其停止移动并暂停发起攻击。"));

        AssertTranslations(
            "item.022.description",
            "Freezes all enemies for 3s, stopping their movement and preventing new attacks.",
            "冻结全部敌人3秒，使其停止移动并暂停发起攻击。");
    }

    /// <summary>Verifies only the star-cluster projectile uses the reduced speed.</summary>
    [Test]
    public void StarClusterSpeedIsExclusive()
    {
        StarClusterActionSO action = Load<StarClusterActionSO>(
            "Assets/_Project/SO/Item/Central/StarClusterActionSO.asset");
        ProjectileProfileSO starClusterProfile = new SerializedObject(action)
            .FindProperty("projectileProfile").objectReferenceValue as ProjectileProfileSO;
        Assert.That(starClusterProfile, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(starClusterProfile),
            Is.EqualTo("Assets/_Project/SO/Item/Projectile/StarClusterProjectileSO.asset"));
        Assert.That(starClusterProfile.SpeedUnitsPerSecond, Is.EqualTo(11.25f));

        ProjectileProfileSO otherHomingProfile = Load<ProjectileProfileSO>(
            "Assets/_Project/SO/Item/Projectile/ProjectileProfile_003.asset");
        Assert.That(otherHomingProfile.TrajectoryDefinition, Is.TypeOf<HomingTrajectoryDefinitionSO>());
        Assert.That(otherHomingProfile.SpeedUnitsPerSecond, Is.EqualTo(22.5f));
    }

    /// <summary>Loads a project asset and fails with its path when it is missing.</summary>
    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, $"Missing asset: {path}");
        return asset;
    }

    /// <summary>Reads an item fallback description without depending on the Editor locale.</summary>
    private static string ReadFallbackDescription(ItemDefinition item)
    {
        return new SerializedObject(item).FindProperty("description").stringValue;
    }

    /// <summary>Checks the English and Simplified Chinese entries for an item description.</summary>
    private static void AssertTranslations(string key, string english, string chinese)
    {
        Assert.That(Load<StringTable>("Assets/_Project/Localization/GameText_en.asset")
            .GetEntry(key).Value, Is.EqualTo(english));
        Assert.That(Load<StringTable>("Assets/_Project/Localization/GameText_zh-Hans.asset")
            .GetEntry(key).Value, Is.EqualTo(chinese));
    }
}
