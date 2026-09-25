using System;
using System.Reflection;
using CameraSystem;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证 Follow 与 Regional 模式独立初始化和故障隔离。
    /// </summary>
    public sealed class SphericalCameraModeIsolationTests
    {
        [Test]
        public void RegionalFailure_DoesNotDeactivateFollowCamera()
        {
            GameObject root = new GameObject("CameraTestRoot");
            try
            {
                SphereWalker walker = CreateWalker(root.transform);
                SphericalCameraFollow follow = CreateFollow(root.transform);
                SphericalCameraRegional regional = root.AddComponent<SphericalCameraRegional>();
                SphericalCameraDirector director = root.AddComponent<SphericalCameraDirector>();

                SetField(director, "_walker", walker);
                SetField(director, "_follow", follow);
                SetField(director, "_regional", regional);
                SetField(director, "_regionCameras", Array.Empty<CinemachineVirtualCamera>());
                InvokePrivate(director, "InitializeModes");

                director.SetMode(SphericalCameraDirector.CameraMode.Follow);
                director.SetMode(SphericalCameraDirector.CameraMode.Regional);

                Assert.That(director.CurrentMode, Is.EqualTo(SphericalCameraDirector.CameraMode.Follow));
                Assert.That(follow.IsActive, Is.True);
                Assert.That(follow.Camera.Priority, Is.EqualTo(20));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EmptyRegionalList_AutoDiscoversRegionalCamera()
        {
            GameObject root = new GameObject("CameraTestRoot");
            try
            {
                SphereWalker walker = CreateWalker(root.transform);
                SphericalCameraRegional regional = root.AddComponent<SphericalCameraRegional>();
                GameObject cameraObject = new GameObject("RegionalCamera");
                cameraObject.transform.SetParent(root.transform);
                cameraObject.transform.position = Vector3.forward * 20f;
                CinemachineVirtualCamera camera = cameraObject.AddComponent<CinemachineVirtualCamera>();
                SphericalCameraDirector director = root.AddComponent<SphericalCameraDirector>();

                SetField(director, "_walker", walker);
                SetField(director, "_regional", regional);
                SetField(director, "_regionCameras", Array.Empty<CinemachineVirtualCamera>());
                InvokePrivate(director, "InitializeModes");
                director.SetMode(SphericalCameraDirector.CameraMode.Regional);

                Assert.That(regional.RegionCameras, Is.EqualTo(new[] { camera }));
                Assert.That(regional.ActiveCamera, Is.SameAs(camera));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static SphereWalker CreateWalker(Transform parent)
        {
            GameObject walkerObject = new GameObject("Walker");
            walkerObject.transform.SetParent(parent);
            walkerObject.transform.position = Vector3.forward * 15f;
            SphereWalker walker = walkerObject.AddComponent<SphereWalker>();
            walker.radius = 15f;
            return walker;
        }

        private static SphericalCameraFollow CreateFollow(Transform parent)
        {
            GameObject followObject = new GameObject("FollowCamera");
            followObject.transform.SetParent(parent);
            followObject.AddComponent<CinemachineVirtualCamera>();
            return followObject.AddComponent<SphericalCameraFollow>();
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"找不到私有方法 {methodName}。");
            method.Invoke(target, null);
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
}
