using System;
using System.Reflection;
using _Project.UI.Damage;
using CombatSystem;
using EnemySystem;
using R3;
using NUnit.Framework;
using Services;
using TMPro;
using UnityEngine;
using Utils;
using VContainer;

public sealed class PoolLifecycleRegressionTests
{
    /// <summary>验证跳字系统不会为同一对象池 Bucket 重建工厂委托。</summary>
    [Test]
    public void DamageNumberSystem_ReusesStablePoolFactory()
    {
        GameObject prefab = CreateDamageNumberPrefab();
        GameObject layerObject = CreateDamageNumberLayer(out DamageNumberLayer layer);
        GameObjectPoolService pool = new();
        DamageEventSource source = new();
        ContainerBuilder builder = new();
        builder.RegisterInstance(layer).AsSelf();
        IObjectResolver resolver = builder.Build();
        DamageNumberSystem system = new(source, pool, resolver);

        try
        {
            MethodInfo rentView = typeof(DamageNumberSystem).GetMethod(
                "RentView",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rentView, Is.Not.Null);

            DamageNumberView viewPrefab = prefab.GetComponent<DamageNumberView>();
            Assert.That(viewPrefab.IsReady, Is.True);
            DamageNumberView first = (DamageNumberView)rentView.Invoke(
                system,
                new object[] { viewPrefab });
            DamageNumberView second = (DamageNumberView)rentView.Invoke(
                system,
                new object[] { viewPrefab });

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
        }
        finally
        {
            system.Dispose();
            resolver.Dispose();
            pool.Dispose();
            UnityEngine.Object.DestroyImmediate(layerObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }
    }

    /// <summary>验证全局对象池不会保留已销毁跳字系统的 factory 委托。</summary>
    [Test]
    public void DamageNumberSystem_ClearsBucketAcrossLifetimes()
    {
        GameObject prefab = CreateDamageNumberPrefab();
        GameObject layerObject = CreateDamageNumberLayer(out DamageNumberLayer layer);
        GameObjectPoolService pool = new();
        DamageEventSource source = new();
        DamageNumberSystem firstSystem = null;
        DamageNumberSystem secondSystem = null;
        IObjectResolver firstResolver = null;
        IObjectResolver secondResolver = null;
        DamageNumberView viewPrefab = prefab.GetComponent<DamageNumberView>();

        try
        {
            Assert.That(viewPrefab.IsReady, Is.True);
            ContainerBuilder firstBuilder = new();
            firstBuilder.RegisterInstance(layer).AsSelf();
            firstResolver = firstBuilder.Build();
            firstSystem = new DamageNumberSystem(source, pool, firstResolver);
            Assert.That(RentView(firstSystem, viewPrefab), Is.Not.Null);

            firstSystem.Dispose();
            firstSystem = null;
            firstResolver.Dispose();
            firstResolver = null;

            PoolUsage usageAfterDispose = pool.GetUsage(prefab);
            Assert.That(usageAfterDispose.TotalCount, Is.EqualTo(0));

            ContainerBuilder secondBuilder = new();
            secondBuilder.RegisterInstance(layer).AsSelf();
            secondResolver = secondBuilder.Build();
            secondSystem = new DamageNumberSystem(source, pool, secondResolver);

            Assert.DoesNotThrow(() =>
            {
                Assert.That(RentView(secondSystem, viewPrefab), Is.Not.Null);
            });
        }
        finally
        {
            secondSystem?.Dispose();
            firstSystem?.Dispose();
            secondResolver?.Dispose();
            firstResolver?.Dispose();
            pool.Dispose();
            UnityEngine.Object.DestroyImmediate(layerObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void ExternallyDestroyedRentedInstance_IsDiscardedBeforeNextRent()
    {
        GameObject prefab = new("PoolPrefab");
        GameObjectPoolService pool = new();

        try
        {
            GameObject first = pool.Rent(prefab, new PoolSettings(0, 2));
            Assert.That(first, Is.Not.Null);

            UnityEngine.Object.DestroyImmediate(first);

            GameObject second = pool.Rent(prefab, new PoolSettings(0, 2));
            PoolUsage usage = pool.GetUsage(prefab);

            Assert.That(second, Is.Not.Null);
            Assert.That(usage.TotalCount, Is.EqualTo(1));
            Assert.That(usage.AvailableCount, Is.EqualTo(0));
            Assert.That(usage.RentedCount, Is.EqualTo(1));
            Assert.That(pool.Return(second), Is.True);
        }
        finally
        {
            pool.Dispose();
            UnityEngine.Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void DestroyingEffectManager_ReturnsUnfinishedEffectOnce()
    {
        GameObject managerObject = new("EffectManager");
        GameObject prefab = new("EffectPrefab");
        EffectSystem.EffectManager manager = managerObject.AddComponent<EffectSystem.EffectManager>();
        SpyPool pool = new();
        IObjectResolver resolver = new ContainerBuilder().Build();

        try
        {
            manager.Construct(pool, resolver);
            GameObject effect = manager.Play(
                prefab,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                0f);

            Assert.That(effect, Is.Not.Null);

            Invoke(manager, "OnDestroy");

            Assert.That(pool.ReturnCount, Is.EqualTo(1));
        }
        finally
        {
            resolver.Dispose();
            pool.Dispose();
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void DestroyingPopulationController_ClearsSpawnerOnlyOnce()
    {
        GameObject controllerObject = new("PopulationController");
        EnemyPopulationController controller = controllerObject.AddComponent<EnemyPopulationController>();
        SpySpawner spawner = new();
        SetField(controller, "_spawner", spawner);

        try
        {
            Invoke(controller, "OnDestroy");

            Assert.That(spawner.ClearAllCallCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    private static GameObject CreateDamageNumberPrefab()
    {
        GameObject prefab = new("DamageNumberView");
        prefab.AddComponent<RectTransform>();
        prefab.AddComponent<CanvasGroup>();
        prefab.AddComponent<DamageNumberView>();
        GameObject label = new("Label");
        label.transform.SetParent(prefab.transform, false);
        label.AddComponent<TextMeshProUGUI>();
        return prefab;
    }

    private static GameObject CreateDamageNumberLayer(out DamageNumberLayer layer)
    {
        GameObject layerObject = new("DamageNumberLayer");
        layerObject.AddComponent<RectTransform>();
        layer = layerObject.AddComponent<DamageNumberLayer>();
        return layerObject;
    }

    private static DamageNumberView RentView(
        DamageNumberSystem system,
        DamageNumberView prefab)
    {
        MethodInfo rentView = typeof(DamageNumberSystem).GetMethod(
            "RentView",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(rentView, Is.Not.Null);
        return (DamageNumberView)rentView.Invoke(system, new object[] { prefab });
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"找不到字段 {name}。");
        field.SetValue(target, value);
    }

    private static void Invoke(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"找不到生命周期方法 {name}。");
        method.Invoke(target, null);
    }

    private sealed class DamageEventSource : IDamageEventSource
    {
        public event Action<DamageEvent> DamageApplied;
    }

    private sealed class SpyPool : IGameObjectPool
    {
        public int ReturnCount { get; private set; }

        public GameObject Rent(
            GameObject prefab,
            PoolSettings settings,
            Transform parent = null,
            bool activate = true,
            Func<GameObject, GameObject> factory = null)
        {
            GameObject instance = factory != null
                ? factory(prefab)
                : UnityEngine.Object.Instantiate(prefab);
            instance.transform.SetParent(parent, false);
            instance.SetActive(activate);
            return instance;
        }

        public bool Return(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            ReturnCount++;
            UnityEngine.Object.DestroyImmediate(instance);
            return true;
        }

        public void Prewarm(
            GameObject prefab,
            PoolSettings settings,
            int count,
            Func<GameObject, GameObject> factory = null)
        {
        }

        public PoolUsage GetUsage(GameObject prefab) => default;

        public void Clear(GameObject prefab)
        {
        }

        public void ClearAll()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class SpySpawner : IEnemySpawner
    {
        public int ClearAllCallCount { get; private set; }

        public int ActiveEnemyCount => 0;
        public Observable<int> OnActiveEnemyCountChanged => null;

        public bool TrySpawnEnemy(
            Enemy prefab,
            EnemySpawnCategory category,
            float healthMultiplier,
            float speedMultiplier)
        {
            return false;
        }

        public int CountCategory(EnemySpawnCategory category) => 0;

        public void ClearAll()
        {
            ClearAllCallCount++;
        }

        public void KillAllEnemies()
        {
        }
    }
}
