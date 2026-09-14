using System.Reflection;
using CameraSystem;
using NUnit.Framework;
using Play;
using UnityEngine;

/// <summary>验证球面圆弧移动、屏幕输入和跨极点的镜头连续性。</summary>
public sealed class SphereTangentMotionTests
{
    /// <summary>从两极前方走过极点，弧长与半径必须保持正确。</summary>
    [TestCase(1f)]
    [TestCase(-1f)]
    public void MoveCrossesPole(float sign)
    {
        GameObject root = new GameObject("WalkerTest");
        try
        {
            SphereWalker walker = root.AddComponent<SphereWalker>();
            walker.sphereCenter = new Vector3(3f, 4f, 5f);
            walker.radius = 10f;
            Vector3 normal = Quaternion.AngleAxis(-sign * 89f, Vector3.right) * Vector3.forward;
            root.transform.position = walker.sphereCenter + normal * walker.radius;
            Vector3 tangent = Vector3.Cross(-sign * Vector3.right, normal);
            MethodInfo move = typeof(SphereWalker).GetMethod("Move", new[] { typeof(Vector3), typeof(float) });
            Assert.That(move, Is.Not.Null, "移动 API 应接收世界切线和弧长");
            move.Invoke(walker, new object[] { tangent, 2f * Mathf.Deg2Rad * walker.radius });
            Vector3 result = root.transform.position - walker.sphereCenter;
            Assert.That(result.magnitude, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(result.z, Is.LessThan(0f));
            Assert.That(Vector3.Angle(normal, result), Is.EqualTo(2f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>输入切线在赤道和两极均保持长度，不放大斜向输入。</summary>
    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(-90f)]
    public void InputRemainsTangent(float angle)
    {
        Vector3 normal = Quaternion.AngleAxis(angle, Vector3.right) * Vector3.forward;
        MethodInfo method = typeof(PlayerController).GetMethod("GetMoveDirection", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        Quaternion cameraRotation = Quaternion.LookRotation(-normal, Vector3.right);
        Vector3 direction = (Vector3)method.Invoke(null, new object[] { new Vector2(1f, 1f), normal, cameraRotation });
        Assert.That(Vector3.Dot(direction, normal), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
        Vector3 right = (Vector3)method.Invoke(null, new object[] { Vector2.right, normal, cameraRotation });
        Assert.That(Vector3.Dot(right, cameraRotation * Vector3.right), Is.GreaterThan(0.999f));
    }

    /// <summary>连续经过南北极时，相机每步只产生对应圆弧的转动。</summary>
    [Test]
    public void FollowKeepsPoleContinuity()
    {
        GameObject player = new GameObject("WalkerTest");
        GameObject cameraObject = new GameObject("CameraTest");
        try
        {
            SphereWalker walker = player.AddComponent<SphereWalker>();
            player.transform.position = Vector3.forward * walker.radius;
            SphericalCameraFollow follow = cameraObject.AddComponent<SphericalCameraFollow>();
            follow.Initialize(walker);
            follow.SetActive(true);
            follow.Tick(0.01f);
            Quaternion previous = cameraObject.transform.rotation;
            for (int angle = 1; angle <= 360; angle++)
            {
                Vector3 normal = Quaternion.AngleAxis(-angle, Vector3.right) * Vector3.forward;
                player.transform.position = normal * walker.radius;
                typeof(SphericalCameraFollow).GetField("_currentDirection", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(follow, normal);
                follow.Tick(0.01f);
                Assert.That(Quaternion.Angle(previous, cameraObject.transform.rotation), Is.LessThan(1.1f), $"角度 {angle}");
                previous = cameraObject.transform.rotation;
            }
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
        }
    }
}
