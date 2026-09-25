using System.Collections.Generic;
using System.Reflection;
using _Project.UI.RunStatus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 验证耐力条的连续当前值、20 段追随值和生命周期行为。
/// </summary>
public sealed class StaminaBarViewTests
{
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<GameObject> _objects = new();

    private StaminaBarView _view;
    private StaminaController _stamina;
    private Slider _backgroundTrack;
    private Slider _trailingFill;
    private Slider _currentFill;
    private GameObject _viewObject;

    [SetUp]
    public void SetUp()
    {
        _viewObject = CreateObject("StaminaBarView");
        _view = _viewObject.AddComponent<StaminaBarView>();
        _backgroundTrack = CreateSlider("BackgroundTrack");
        _trailingFill = CreateSlider("TrailingFill");
        _currentFill = CreateSlider("CurrentFill");

        SetField(_view, "_backgroundTrack", _backgroundTrack);
        SetField(_view, "_trailingFill", _trailingFill);
        SetField(_view, "_currentFill", _currentFill);

        GameObject staminaObject = CreateObject("StaminaController");
        _stamina = staminaObject.AddComponent<StaminaController>();
        _stamina.maxStamina = 100f;
        SetField(_stamina, "<currentStamina>k__BackingField", 100f);
        SetField(_view, "_stamina", _stamina);

        Invoke(_stamina, "Start");
        Invoke(_view, "Start");
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = _objects.Count - 1; index >= 0; index--)
        {
            if (_objects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(_objects[index]);
            }
        }

        _objects.Clear();
    }

    [Test]
    public void InitialValue_SynchronizesAllLayers()
    {
        Assert.That(_backgroundTrack.value, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(_currentFill.value, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(_trailingFill.value, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void StaminaState_UpdatesViewThroughR3()
    {
        _stamina.drainRate = 10f;
        Invoke(_stamina, "Tick", 1f, true);

        Assert.That(_currentFill.value, Is.EqualTo(0.9f).Within(0.0001f));
        Assert.That(_trailingFill.value, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void CurrentValue_RemainsContinuousWhileTrailingValueUsesSegments()
    {
        Invoke(_view, "HandleStaminaChanged", 46f);

        Assert.That(_currentFill.value, Is.EqualTo(0.46f).Within(0.0001f));
        Assert.That(_trailingFill.value, Is.EqualTo(1f).Within(0.0001f));

        Invoke(_view, "AdvanceFollow", 0.25f);

        Assert.That(_trailingFill.value, Is.EqualTo(19f / 20f).Within(0.0001f));
    }

    [Test]
    public void TrailingValue_MovesOneSegmentPerInterval()
    {
        Invoke(_view, "HandleStaminaChanged", 46f);
        Invoke(_view, "AdvanceFollow", 0.25f);

        Assert.That(_trailingFill.value, Is.EqualTo(19f / 20f).Within(0.0001f));

        Invoke(_view, "AdvanceFollow", 0.25f);

        Assert.That(_trailingFill.value, Is.EqualTo(18f / 20f).Within(0.0001f));
    }

    [Test]
    public void TrailingValue_ReachesTargetWithoutSkippingSegments()
    {
        Invoke(_view, "HandleStaminaChanged", 46f);

        for (int step = 0; step < 11; step++)
        {
            Invoke(_view, "AdvanceFollow", 0.25f);
        }

        Assert.That(_trailingFill.value, Is.EqualTo(10f / 20f).Within(0.0001f));
    }

    [Test]
    public void TrailingValue_UpdatesImmediatelyWhenStaminaRecovers()
    {
        Invoke(_view, "HandleStaminaChanged", 10f);
        for (int step = 0; step < 18; step++)
        {
            Invoke(_view, "AdvanceFollow", 0.25f);
        }

        Invoke(_view, "HandleStaminaChanged", 46f);

        Assert.That(_currentFill.value, Is.EqualTo(0.46f).Within(0.0001f));
        Assert.That(_trailingFill.value, Is.EqualTo(10f / 20f).Within(0.0001f));
    }

    [Test]
    public void InvalidStaminaValues_RenderAsZero()
    {
        Invoke(_view, "HandleStaminaChanged", float.NaN);
        Assert.That(_currentFill.value, Is.EqualTo(0f));

        _stamina.maxStamina = 0f;
        Invoke(_view, "HandleStaminaChanged", 50f);
        Assert.That(_currentFill.value, Is.EqualTo(0f));

        _stamina.maxStamina = float.PositiveInfinity;
        Invoke(_view, "HandleStaminaChanged", 50f);
        Assert.That(_currentFill.value, Is.EqualTo(0f));
    }

    [Test]
    public void ZeroFollowSpeed_DoesNotAdvanceTrailingValue()
    {
        SetField(_view, "_trailingFillSpeed", 0f);
        Invoke(_view, "HandleStaminaChanged", 46f);
        Invoke(_view, "AdvanceFollow", 10f);

        Assert.That(_trailingFill.value, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void MissingLayer_DisablesViewDuringInitialization()
    {
        GameObject objectWithoutLayers = CreateObject("InvalidStaminaBarView");
        StaminaBarView invalidView = objectWithoutLayers.AddComponent<StaminaBarView>();

        Invoke(invalidView, "Start");

        Assert.That(invalidView.enabled, Is.False);
    }

    [Test]
    public void DestroyingView_DisposesR3Subscription()
    {
        FieldInfo subscriptionField = typeof(StaminaBarView).GetField(
            "_staminaSubscription",
            PrivateInstance);
        Assert.That(subscriptionField, Is.Not.Null);
        Assert.That(subscriptionField.GetValue(_view), Is.Not.Null);

        _objects.Remove(_viewObject);
        UnityEngine.Object.DestroyImmediate(_viewObject);
        _viewObject = null;

        Assert.That(subscriptionField.GetValue(_view), Is.Null);
    }

    private GameObject CreateObject(string objectName)
    {
        GameObject created = new(objectName);
        _objects.Add(created);
        return created;
    }

    private Slider CreateSlider(string objectName)
    {
        return CreateObject(objectName).AddComponent<Slider>();
    }

    private static void Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            PrivateInstance);
        Assert.That(method, Is.Not.Null, $"找不到私有方法 {methodName}。");
        method.Invoke(target, arguments);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"找不到私有字段 {fieldName}。");
        field.SetValue(target, value);
    }
}
