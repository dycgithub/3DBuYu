using System.Collections.Generic;
using CombatSystem;
using EffectSystem;
using Interfaces;
using NUnit.Framework;
using Play;
using UnityEditor;
using UnityEngine;
using Utils;

public sealed class CombatVisualContractTests
{
    private const string BulletProfilePath = "Assets/_Project/SO/Bullet/DefaultBulletProfile.asset";
    private const string BulletVisualPath = "Assets/_Project/SO/Bullet/DefaultBulletVisual.asset";
    private const string BulletPrefabPath = "Assets/_Project/Prefabs/Player/Bullet.prefab";
    private const string PortFolder = "Assets/_Project/SO/Port";
    private const string EffectCatalogPath = "Assets/_Project/SO/Effects/CombatEffectCatalog.asset";

    private static readonly EffectId[] RequiredEffects =
    {
        EffectId.EnemyHit,
        EffectId.EnemyDeath,
        EffectId.ShieldHit,
        EffectId.EnemyDodge,
        EffectId.BigExplosion,
        EffectId.PlayerDamage,
        EffectId.BulletHit,
        EffectId.BulletExpired
    };

    [Test]
    public void DefaultBulletProfile_HasAVisiblePrefab()
    {
        BulletProfile profile = AssetDatabase.LoadAssetAtPath<BulletProfile>(BulletProfilePath);
        BulletVisualDefinition visual = AssetDatabase.LoadAssetAtPath<BulletVisualDefinition>(BulletVisualPath);
        GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);

        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.Visual, Is.SameAs(visual));
        Assert.That(profile.Visual.Prefab, Is.SameAs(bulletPrefab));
        Assert.That(profile.Visual.Prefab.GetComponentInChildren<Renderer>(true), Is.Not.Null);
    }

    [Test]
    public void EveryPortConfig_UsesTheDefaultBulletProfile()
    {
        BulletProfile profile = AssetDatabase.LoadAssetAtPath<BulletProfile>(BulletProfilePath);
        string[] guids = AssetDatabase.FindAssets("t:TransmitterSO", new[] { PortFolder });

        Assert.That(guids, Has.Length.EqualTo(8));
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TransmitterSO port = AssetDatabase.LoadAssetAtPath<TransmitterSO>(path);
            Assert.That(port, Is.Not.Null, path);
            Assert.That(port.defaultBullet, Is.SameAs(profile), path);
        }
    }

    [Test]
    public void PlayerContainer_UsesTheDefaultBulletProfile()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerContainer.prefab");
        TransmitterFireController controller = prefab.GetComponentInChildren<TransmitterFireController>(true);

        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.bulletProfile, Is.SameAs(
            AssetDatabase.LoadAssetAtPath<BulletProfile>(BulletProfilePath)));
    }

    [Test]
    public void CombatEffectCatalog_ContainsEveryRequiredEffect()
    {
        CombatEffectCatalogSO catalog = AssetDatabase.LoadAssetAtPath<CombatEffectCatalogSO>(EffectCatalogPath);

        Assert.That(catalog, Is.Not.Null);
        foreach (EffectId effectId in RequiredEffects)
        {
            Assert.That(catalog.TryGet(effectId, out EffectCatalogEntry entry), Is.True, effectId.ToString());
            Assert.That(entry.Prefab, Is.Not.Null, effectId.ToString());
            Assert.That(entry.MaximumRetained, Is.GreaterThanOrEqualTo(entry.PrewarmCount), effectId.ToString());
        }
    }

    [Test]
    public void GameObjectPool_RejectsDuplicateReturn()
    {
        GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prefab.SetActive(false);
        GameObjectPoolService pool = new GameObjectPoolService();

        try
        {
            GameObject instance = pool.Rent(prefab, new PoolSettings(0, 1));

            Assert.That(instance, Is.Not.Null);
            Assert.That(pool.Return(instance), Is.True);
            Assert.That(pool.Return(instance), Is.False);
            Assert.That(pool.GetUsage(prefab).RentedCount, Is.Zero);
        }
        finally
        {
            pool.Dispose();
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void DestroyedUnityDamageable_IsRejectedBeforeIsAliveIsRead()
    {
        GameObject targetObject = new GameObject("Damageable");
        IDamageable target = targetObject.AddComponent<TestUnityDamageable>();

        Object.DestroyImmediate(targetObject);

        Assert.That(target.IsAliveAndValid(), Is.False);
    }

    private sealed class TestUnityDamageable : MonoBehaviour, IDamageable
    {
        public Vector3 Position => transform.position;
        public bool IsAlive => gameObject.activeInHierarchy;
        public Transform Transform => transform;

        public void TakeDamage(float amount)
        {
        }
    }
}
