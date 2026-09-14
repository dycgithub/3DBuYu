using System.IO;
using System.Reflection;
using NUnit.Framework;
using Play;
using UnityEngine;
using VContainer;

/// <summary>锁定战斗场景玩家必须使用当前 GameScene 的相机依赖。</summary>
public sealed class PlayerControllerSceneTransitionTests
{
    /// <summary>玩家移动相机必须由 VContainer 注入，避免场景切换时取错 Camera.main。</summary>
    [Test]
    public void MovementCamera_IsInjected()
    {
        FieldInfo field = typeof(PlayerController).GetField(
            "_movementCamera",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null);
        Assert.That(field.GetCustomAttribute<InjectAttribute>(), Is.Not.Null);
    }

    /// <summary>GameLoopLifetimeScope 必须从自身场景注册唯一的战斗相机。</summary>
    [Test]
    public void GameLoopScope_RegistersSceneCamera()
    {
        string path = Path.Combine(
            Application.dataPath,
            "_Project/Scripts/Composition/Scopes/GameLoopLifetimeScope.cs");
        string source = File.ReadAllText(path);

        Assert.That(
            source,
            Does.Contain("builder.RegisterComponentInHierarchy<Camera>().AsSelf();"));
    }
}
