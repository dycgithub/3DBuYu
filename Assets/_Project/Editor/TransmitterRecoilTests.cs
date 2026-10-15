using System.Reflection;
using NUnit.Framework;
using Play;
using UnityEditor;
using UnityEngine;

/// <summary>验证后坐的 1.5% 默认幅度、Prefab 可调参数和模型局部位移边界。</summary>
public sealed class TransmitterRecoilTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const float ExpectedFraction = 0.015f;

    private GameObject _port;
    private GameObject _model;
    private TransmitterRecoil _recoil;
    private Vector3 _basePosition;
    private float _maxDistance;

    /// <summary>创建隔离端口、模型和可测渲染器。</summary>
    [SetUp]
    public void SetUp()
    {
        _port = new GameObject("RecoilTestPort");
        _port.transform.position = new Vector3(3f, -2f, 7f);
        _model = new GameObject("RecoilTestModel");
        _model.transform.SetParent(_port.transform, false);
        _model.transform.localPosition = new Vector3(0.2f, 0.1f, -0.3f);
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(_model.transform, false);
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        _recoil = _model.AddComponent<TransmitterRecoil>();
        InvokePrivate(_recoil, "Awake");
        _basePosition = _model.transform.localPosition;
        _maxDistance = visual.GetComponent<Renderer>().bounds.size.z * ExpectedFraction;
    }

    /// <summary>销毁本测试创建的模型和端口，不触碰活动场景对象。</summary>
    [TearDown]
    public void TearDown()
    {
        if (_port != null)
        {
            Object.DestroyImmediate(_port);
        }
    }

    /// <summary>非均匀父缩放下仍沿世界发射方向反向移动指定比例。</summary>
    [TestCase(1f, 1f, 1f)]
    [TestCase(2f, 2f, 2f)]
    [TestCase(2f, 3f, 4f)]
    public void KickMovesOppositeDirectionInWorldSpace(float x, float y, float z)
    {
        _port.transform.localScale = new Vector3(x, y, z);
        InvokePrivate(_recoil, "Awake");
        Vector3 launchDirection = new Vector3(0.3f, 0.4f, 0.8f).normalized;
        Bounds modelBounds = _model.GetComponentInChildren<Renderer>().bounds;
        Vector3 absoluteDirection = new Vector3(
            Mathf.Abs(launchDirection.x),
            Mathf.Abs(launchDirection.y),
            Mathf.Abs(launchDirection.z));
        _maxDistance = Vector3.Dot(modelBounds.extents, absoluteDirection) * 2f * ExpectedFraction;
        Vector3 firePointPosition = _port.transform.position;
        Quaternion firePointRotation = _port.transform.rotation;
        Vector3 firePointScale = _port.transform.localScale;

        _recoil.Kick(launchDirection);
        Step(0.04f);

        Vector3 worldOffset = _model.transform.position - _port.transform.TransformPoint(_basePosition);
        Assert.That(Vector3.Dot(worldOffset.normalized, launchDirection), Is.LessThan(-0.999f));
        Assert.That(worldOffset.magnitude, Is.EqualTo(_maxDistance).Within(0.0001f));
        Assert.That(_port.transform.position, Is.EqualTo(firePointPosition));
        Assert.That(_port.transform.rotation, Is.EqualTo(firePointRotation));
        Assert.That(_port.transform.localScale, Is.EqualTo(firePointScale));
    }

    /// <summary>连续同帧触发不会提前移动模型或超过世界空间位移上限。</summary>
    [Test]
    public void RepeatedKickStaysContinuousAndBounded()
    {
        _recoil.Kick(Vector3.forward);
        _recoil.Kick(Vector3.forward);
        Assert.That(_model.transform.localPosition, Is.EqualTo(_basePosition));

        for (int index = 0; index < 8; index++)
        {
            _recoil.Kick(Vector3.forward);
            Step(0.04f);
        }

        Vector3 worldOffset = _model.transform.position - _port.transform.TransformPoint(_basePosition);
        Assert.That(worldOffset.magnitude, Is.LessThanOrEqualTo(_maxDistance + 0.0001f));
    }

    /// <summary>停火后模型在回位时长内恢复原始局部位置。</summary>
    [Test]
    public void KickReturnsToBasePosition()
    {
        _recoil.Kick(Vector3.forward);
        Step(0.04f);
        Step(0.14f);

        Assert.That(_model.transform.localPosition, Is.EqualTo(_basePosition));
    }

    /// <summary>TimeScale 为零时 Update 不推进后坐状态。</summary>
    [Test]
    public void PausedUpdateKeepsCurrentPosition()
    {
        _recoil.Kick(Vector3.forward);
        Step(0.02f);
        Vector3 pausedPosition = _model.transform.localPosition;
        float previousTimeScale = Time.timeScale;

        try
        {
            Time.timeScale = 0f;
            InvokePrivate(_recoil, "Update");
            Assert.That(_model.transform.localPosition, Is.EqualTo(pausedPosition));
        }
        finally
        {
            Time.timeScale = previousTimeScale;
        }
    }

    /// <summary>禁用组件会清除已有偏移并复位模型。</summary>
    [Test]
    public void DisableResetsModelPosition()
    {
        _recoil.Kick(Vector3.forward);
        Step(0.02f);
        InvokePrivate(_recoil, "OnDisable");

        Assert.That(_model.transform.localPosition, Is.EqualTo(_basePosition));
    }

    /// <summary>正常及锁定模型预挂后坐组件，使三个参数可在 Prefab Inspector 中持久配置。</summary>
    [TestCase("Transmitter")]
    [TestCase("TransmitterLock")]
    public void PrefabHasAuthoredRecoil(string prefabName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/Player/" + prefabName + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        TransmitterRecoil recoil = prefab.GetComponent<TransmitterRecoil>();
        Assert.That(recoil, Is.Not.Null);
        SerializedObject data = new(recoil);
        Assert.That(data.FindProperty("_modelLengthFraction").floatValue,
            Is.EqualTo(ExpectedFraction).Within(0.00001f));
        Assert.That(data.FindProperty("_kickDuration").floatValue,
            Is.EqualTo(0.02f).Within(0.00001f));
        Assert.That(data.FindProperty("_returnDuration").floatValue,
            Is.EqualTo(0.05f).Within(0.00001f));
    }

    /// <summary>预挂组件在端口重设模型位置后仍回到端口原点，不恢复 Prefab 的资产位置偏移。</summary>
    [Test]
    public void AuthoredModelReturnsToOrigin()
    {
        TransmitterSO config = AssetDatabase.LoadAssetAtPath<TransmitterSO>(
            "Assets/_Project/SO/Transmitter/PortConfig_Top.asset");
        Assert.That(config, Is.Not.Null);
        GameObject model = Object.Instantiate(config.portModelPrefab, _port.transform);
        try
        {
            TransmitterRecoil recoil = model.GetComponent<TransmitterRecoil>();
            Assert.That(recoil, Is.Not.Null);
            InvokePrivate(recoil, "Awake");
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            InvokePrivate(recoil, "Start");
            Vector3 firePointPosition = _port.transform.position;
            recoil.Kick(Vector3.forward);
            InvokePrivate(recoil, "Step", 0.02f);
            InvokePrivate(recoil, "Step", 0.05f);
            Assert.That(recoil.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_port.transform.position, Is.EqualTo(firePointPosition));
        }
        finally
        {
            Object.DestroyImmediate(model);
        }
    }

    /// <summary>经私有时间步接缝推进组件状态。</summary>
    /// <param name="deltaTime">测试帧长。</param>
    private void Step(float deltaTime)
    {
        InvokePrivate(_recoil, "Step", deltaTime);
    }

    /// <summary>通过反射调用生产组件私有生命周期或时间步函数。</summary>
    /// <param name="target">目标组件。</param>
    /// <param name="methodName">私有函数名。</param>
    /// <param name="arguments">函数参数。</param>
    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, PrivateInstance);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, arguments);
    }
}
