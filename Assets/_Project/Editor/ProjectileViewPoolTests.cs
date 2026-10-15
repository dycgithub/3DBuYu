using System.Reflection;
using CombatSystem;
using NUnit.Framework;
using UnityEngine;
using Utils;

/// <summary>覆盖投射物视觉的未激活准备、拖尾复用清理与激光双线生命周期。</summary>
public sealed class ProjectileViewPoolTests
{
    private GameObject _prefab;
    private ProjectileProfileSO _profile;
    private ProjectileVisualDefinitionSO _visual;
    private GameObjectPoolService _gameObjectPool;
    private ProjectileViewPool _viewPool;
    private GameObject _beamPrefab;
    private LaserTrajectoryDefinitionSO _laser;

    /// <summary>构造一个只包含视觉引用的弹体配置。</summary>
    [SetUp]
    public void SetUp()
    {
        _prefab = new GameObject("ProjectileViewPoolTestPrefab");
        _prefab.AddComponent<TrailRenderer>();
        _prefab.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.4f, 0.3f);

        GameObject body = new GameObject("Body");
        body.transform.SetParent(_prefab.transform, false);

        _visual = ScriptableObject.CreateInstance<ProjectileVisualDefinitionSO>();
        _visual.ProjectilePrefab = _prefab;
        _visual.ProjectileLengthMultiplier = 1.5f;
        _visual.ProjectileThicknessMultiplier = 0.75f;
        _visual.TrailTimeSeconds = 0.12f;
        _visual.TrailWidth = 0.025f;
        _visual.TrailColor = Color.cyan;
        _profile = ScriptableObject.CreateInstance<ProjectileProfileSO>();
        _profile.VisualDefinition = _visual;

        _gameObjectPool = new GameObjectPoolService();
        _viewPool = new ProjectileViewPool(_gameObjectPool);
    }

    /// <summary>释放测试创建的对象池、配置对象与预制体。</summary>
    [TearDown]
    public void TearDown()
    {
        _viewPool?.Dispose();
        _gameObjectPool?.Dispose();

        if (_laser != null)
        {
            Object.DestroyImmediate(_laser);
        }

        if (_beamPrefab != null)
        {
            Object.DestroyImmediate(_beamPrefab);
        }

        if (_profile != null)
        {
            Object.DestroyImmediate(_profile);
        }

        if (_visual != null)
        {
            Object.DestroyImmediate(_visual);
        }

        if (_prefab != null)
        {
            Object.DestroyImmediate(_prefab);
        }
    }

    /// <summary>租借期间先定位与配置，再由批次激活；旋转按角度比较以容忍 Unity 归一化。</summary>
    [Test]
    public void RentPreparesBeforeActivation()
    {
        Vector3 position = new(3f, 4f, 5f);
        Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);

        GameObject instance = _viewPool.RentProjectileView(_profile, position, rotation);

        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.activeSelf, Is.False);
        Assert.That(instance.transform.position, Is.EqualTo(position));
        Assert.That(Quaternion.Angle(instance.transform.rotation, rotation), Is.LessThan(0.001f));

        Transform body = instance.transform.GetChild(0);
        Assert.That(body.localScale, Is.EqualTo(new Vector3(0.75f, 1.5f, 0.75f)));

        TrailRenderer trail = instance.GetComponent<TrailRenderer>();
        Assert.That(trail.emitting, Is.True);
        Assert.That(trail.time, Is.EqualTo(0.12f));
        Assert.That(trail.widthMultiplier, Is.EqualTo(0.025f));
        Assert.That(trail.startColor, Is.EqualTo(Color.cyan));
        Assert.That(trail.endColor, Is.EqualTo(new Color(0f, 1f, 1f, 0f)));
        Assert.That(instance.GetComponent<BoxCollider>().size, Is.EqualTo(new Vector3(0.5f, 0.4f, 0.3f)));

        trail.AddPositions(new[] { Vector3.zero, Vector3.right });
        _viewPool.ReturnProjectileView(instance);

        Assert.That(trail.emitting, Is.False);
        Assert.That(trail.positionCount, Is.Zero);

        GameObject reused = _viewPool.RentProjectileView(
            _profile,
            Vector3.left,
            Quaternion.identity);
        Assert.That(reused, Is.SameAs(instance));
        Assert.That(trail.positionCount, Is.Zero);
        Assert.That(trail.emitting, Is.True);

        _viewPool.ReturnProjectileView(reused);
    }

    /// <summary>激光亮芯与外束共享连续端点，并在归还及复租时清空。</summary>
    [Test]
    public void LaserCoreClearsOnReuse()
    {
        _beamPrefab = new GameObject("LaserBeamTestPrefab");
        LineRenderer outer = _beamPrefab.AddComponent<LineRenderer>();
        GameObject coreObject = new("BrightCore");
        coreObject.transform.SetParent(_beamPrefab.transform, false);
        LineRenderer core = coreObject.AddComponent<LineRenderer>();
        _beamPrefab.AddComponent<LaserBeamView>();

        _laser = ScriptableObject.CreateInstance<LaserTrajectoryDefinitionSO>();
        _laser.BeamPrefab = _beamPrefab;
        _profile.TrajectoryDefinition = _laser;

        GameObject instance = _viewPool.RentProjectileView(
            _profile,
            Vector3.zero,
            Quaternion.identity);
        LaserBeamView view = instance.GetComponent<LaserBeamView>();
        LineRenderer[] lines = instance.GetComponentsInChildren<LineRenderer>(true);
        Assert.That(lines, Has.Length.EqualTo(2));
        LineRenderer rentedOuter = lines[0];
        LineRenderer rentedCore = lines[1];
        Assert.That(rentedOuter.enabled, Is.True);
        Assert.That(rentedCore.enabled, Is.True);
        Assert.That(rentedCore.startWidth, Is.LessThan(rentedOuter.startWidth));

        var beamProperties = new MaterialPropertyBlock();
        rentedOuter.GetPropertyBlock(beamProperties);
        Assert.That(
            beamProperties.GetFloat(Shader.PropertyToID("_SegmentCount")),
            Is.EqualTo(1f));
        rentedCore.GetPropertyBlock(beamProperties);
        Assert.That(
            beamProperties.GetFloat(Shader.PropertyToID("_SegmentLength")),
            Is.EqualTo(1f));

        Vector3 start = new(1f, 2f, 3f);
        Vector3 end = new(4f, 5f, 6f);
        MethodInfo setBeam = typeof(LaserBeamView).GetMethod(
            "SetBeam",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setBeam, Is.Not.Null);
        setBeam.Invoke(view, new object[] { start, end });

        Assert.That(rentedOuter.positionCount, Is.EqualTo(2));
        Assert.That(rentedCore.positionCount, Is.EqualTo(2));
        Assert.That(rentedOuter.GetPosition(0), Is.EqualTo(start));
        Assert.That(rentedOuter.GetPosition(1), Is.EqualTo(end));
        Assert.That(rentedCore.GetPosition(0), Is.EqualTo(start));
        Assert.That(rentedCore.GetPosition(1), Is.EqualTo(end));

        _viewPool.ReturnProjectileView(instance);
        Assert.That(rentedOuter.enabled, Is.False);
        Assert.That(rentedCore.enabled, Is.False);
        Assert.That(rentedOuter.positionCount, Is.Zero);
        Assert.That(rentedCore.positionCount, Is.Zero);

        GameObject reused = _viewPool.RentProjectileView(
            _profile,
            Vector3.right,
            Quaternion.identity);
        Assert.That(reused, Is.SameAs(instance));
        Assert.That(rentedOuter.enabled, Is.True);
        Assert.That(rentedCore.enabled, Is.True);
        Assert.That(rentedOuter.positionCount, Is.Zero);
        Assert.That(rentedCore.positionCount, Is.Zero);

        _viewPool.ReturnProjectileView(reused);
    }
}
