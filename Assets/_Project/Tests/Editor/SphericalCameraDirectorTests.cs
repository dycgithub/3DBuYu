using System;
using System.Reflection;
using CameraSystem;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;

public sealed class SphericalCameraDirectorTests
{
    [Test]
    public void Follow_CameraPositionLiesOnConfiguredOuterSphere()
    {
        Vector3 position = InvokeFollowMethod<Vector3>(
            "CalculateCameraPosition",
            Vector3.zero,
            Vector3.up,
            37.5f);

        Assert.That(position, Is.EqualTo(new Vector3(0f, 37.5f, 0f)));
    }

    [Test]
    public void Follow_SmoothingUsesTimeConstant()
    {
        Vector3 current = Vector3.forward;
        Vector3 target = Vector3.right;

        Vector3 result = InvokeFollowMethod<Vector3>(
            "SmoothDirection",
            current,
            target,
            0.1f,
            0.5f);

        float expectedAngle = 90f * (1f - Mathf.Exp(-0.1f / 0.5f));
        Assert.That(Vector3.Angle(result, current), Is.EqualTo(expectedAngle).Within(0.001f));
        Assert.That(Vector3.Angle(result, target), Is.GreaterThan(0f));
    }

    [Test]
    public void Regional_SelectsClosestRegionBySurfaceNormal()
    {
        Vector3[] regions =
        {
            Vector3.forward,
            Vector3.right,
            Vector3.back
        };

        int selected = InvokeRegionalMethod<int>(
            "SelectRegionIndex",
            Vector3.right,
            regions,
            -1,
            0f);

        Assert.That(selected, Is.EqualTo(1));
    }

    [Test]
    public void Regional_HysteresisPreventsBoundarySwitch()
    {
        Vector3[] regions =
        {
            Vector3.forward,
            Quaternion.Euler(0f, 80f, 0f) * Vector3.forward
        };

        int selected = InvokeRegionalMethod<int>(
            "SelectRegionIndex",
            Quaternion.Euler(0f, 39f, 0f) * Vector3.forward,
            regions,
            0,
            5f);

        Assert.That(selected, Is.EqualTo(0));
    }

    [Test]
    public void Director_RegionalModeActivatesSelectedRegion()
    {
        GameObject root = new GameObject("CameraTestRoot");
        GameObject walkerObject = new GameObject("Walker");
        GameObject firstCameraObject = new GameObject("RegionA");
        GameObject secondCameraObject = new GameObject("RegionB");

        try
        {
            SphereWalker walker = walkerObject.AddComponent<SphereWalker>();
            walker.radius = 10f;
            walker.transform.position = Vector3.forward * walker.radius;

            CinemachineVirtualCamera firstCamera = firstCameraObject.AddComponent<CinemachineVirtualCamera>();
            firstCamera.transform.position = Vector3.forward * 15f;
            CinemachineVirtualCamera secondCamera = secondCameraObject.AddComponent<CinemachineVirtualCamera>();
            secondCamera.transform.position = Vector3.right * 15f;

            SphericalCameraRegional regional = root.AddComponent<SphericalCameraRegional>();
            SetField(regional, "_walker", walker);
            SetField(regional, "_regionCameras", new[] { firstCamera, secondCamera });

            SphericalCameraDirector director = root.AddComponent<SphericalCameraDirector>();
            SetField(director, "_regional", regional);
            SetField(director, "_activePriority", 20);
            SetField(director, "_inactivePriority", 0);

            director.SetMode(SphericalCameraDirector.CameraMode.Regional);

            Assert.That(regional.CurrentRegionIndex, Is.EqualTo(0));
            Assert.That(firstCamera.Priority, Is.EqualTo(20));
            Assert.That(secondCamera.Priority, Is.EqualTo(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(walkerObject);
            UnityEngine.Object.DestroyImmediate(firstCameraObject);
            UnityEngine.Object.DestroyImmediate(secondCameraObject);
        }
    }

    private static T InvokeFollowMethod<T>(string methodName, params object[] arguments)
    {
        return InvokeStatic<T>("CameraSystem.SphericalCameraFollow", methodName, arguments);
    }

    private static T InvokeRegionalMethod<T>(string methodName, params object[] arguments)
    {
        return InvokeStatic<T>("CameraSystem.SphericalCameraRegional", methodName, arguments);
    }

    private static T InvokeStatic<T>(string typeName, string methodName, params object[] arguments)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"找不到类型 {typeName}。");

        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, $"找不到公开静态方法 {methodName}。");

        return (T)method.Invoke(null, arguments);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"找不到字段 {fieldName}。");
        field.SetValue(target, value);
    }
}
