using System.Collections.Generic;
using System.Reflection;
using EffectSystem;
using NUnit.Framework;
using UnityEngine;
using Utils;
using VContainer;

public sealed class EffectCatalogScaleTests
{
    [Test]
    public void CatalogScaleIsAppliedToDirectoryEffect()
    {
        GameObject managerObject = new("EffectManager");
        GameObject prefab = new("EffectPrefab");
        ParticleSystem particleSystem = prefab.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        CombatEffectCatalogSO catalog = ScriptableObject.CreateInstance<CombatEffectCatalogSO>();
        GameObjectPoolService pool = new();
        EffectManager manager = managerObject.AddComponent<EffectManager>();
        IObjectResolver resolver = null;
        GameObject effect = null;

        try
        {
            SetEntries(catalog, new EffectCatalogEntry
            {
                Id = EffectId.TransmitterFire,
                Prefab = prefab,
                Scale = 0.3f,
                MaximumRetained = 1
            });
            resolver = CreateResolver(catalog);
            manager.Construct(pool, resolver);

            effect = manager.PlayEffect(EffectId.TransmitterFire, Vector3.zero);

            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.transform.localScale, Is.EqualTo(Vector3.one * 0.3f));
        }
        finally
        {
            if (effect != null)
            {
                manager.Stop(effect);
            }

            resolver?.Dispose();
            pool.Dispose();
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void ParentEffectUsesLocalPoseAndDetachesOnStop()
    {
        GameObject managerObject = new("EffectManager");
        GameObject prefab = new("EffectPrefab");
        ParticleSystem particleSystem = prefab.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        GameObject parent = new("FirePoint");
        parent.transform.SetPositionAndRotation(
            new Vector3(3f, 4f, 5f),
            Quaternion.Euler(0f, 90f, 0f));
        CombatEffectCatalogSO catalog = ScriptableObject.CreateInstance<CombatEffectCatalogSO>();
        GameObjectPoolService pool = new();
        EffectManager manager = managerObject.AddComponent<EffectManager>();
        IObjectResolver resolver = null;
        GameObject effect = null;

        try
        {
            SetEntries(catalog, new EffectCatalogEntry
            {
                Id = EffectId.TransmitterFire,
                Prefab = prefab,
                MaximumRetained = 1
            });
            resolver = CreateResolver(catalog);
            manager.Construct(pool, resolver);

            effect = manager.PlayEffect(EffectId.TransmitterFire, parent.transform);

            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.transform.parent, Is.SameAs(parent.transform));
            Assert.That(effect.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(effect.transform.localRotation, Is.EqualTo(Quaternion.identity));

            manager.Stop(effect);

            Assert.That(effect.transform.parent, Is.Null);
        }
        finally
        {
            resolver?.Dispose();
            pool.Dispose();
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(prefab);
        }
    }

    private static IObjectResolver CreateResolver(CombatEffectCatalogSO catalog)
    {
        ContainerBuilder builder = new();
        builder.RegisterInstance(catalog).AsSelf();
        return builder.Build();
    }

    private static void SetEntries(
        CombatEffectCatalogSO catalog,
        EffectCatalogEntry entry)
    {
        FieldInfo field = typeof(CombatEffectCatalogSO).GetField(
            "_entries",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(catalog, new List<EffectCatalogEntry> { entry });
    }
}
