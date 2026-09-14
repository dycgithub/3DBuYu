using System;
using System.Collections.Generic;
using System.Reflection;
using GameSystem;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using _Project.UI.RunStatus;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证连杀视图只使用 Hierarchy 中已配置的 UI 对象。
    /// </summary>
    public sealed class KillStreakViewReferenceTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void View_WithMissingReferences_DoesNotCreateChildren()
        {
            GameObject root = CreateObject("KillStreakRoot");
            KillStreakView view = root.AddComponent<KillStreakView>();
            InvokeLifecycle(view, "Awake");

            Assert.That(root.transform.childCount, Is.Zero);
        }

        [Test]
        public void View_WithConfiguredReferences_UpdatesLabelsWithoutCreatingChildren()
        {
            GameObject root = CreateObject("KillStreakRoot");
            GameObject overlay = CreateObject("KillStreakOverlay", root.transform);
            CanvasGroup canvasGroup = overlay.AddComponent<CanvasGroup>();
            TextMeshProUGUI currentLabel = CreateLabel("CurrentStreak", overlay.transform);
            TextMeshProUGUI bestLabel = CreateLabel("BestStreak", overlay.transform);
            KillStreakView view = root.AddComponent<KillStreakView>();
            InvokeLifecycle(view, "Awake");
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            int childCountBeforeStart = root.transform.childCount;
            KillStreakService service = new();

            try
            {
                SetField(view, "_canvasGroup", canvasGroup);
                SetField(view, "_currentLabel", currentLabel);
                SetField(view, "_bestLabel", bestLabel);
                SetField(view, "_killStreak", service);
                typeof(KillStreakView)
                    .GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(view, null);

                service.BeginRun(3f);
                service.RegisterKill();
                service.RegisterKill();

                Assert.That(root.transform.childCount, Is.EqualTo(childCountBeforeStart));
                Assert.That(currentLabel.text, Is.EqualTo("连杀 ×2"));
                Assert.That(bestLabel.text, Is.EqualTo("最高 ×2"));
            }
            finally
            {
                service.Dispose();
            }
        }

        private GameObject CreateObject(string objectName, Transform parent = null)
        {
            GameObject createdObject = new(objectName);
            if (parent != null)
            {
                createdObject.transform.SetParent(parent, false);
            }

            _createdObjects.Add(createdObject);
            return createdObject;
        }

        private TextMeshProUGUI CreateLabel(string objectName, Transform parent)
        {
            GameObject labelObject = CreateObject(objectName, parent);
            return labelObject.AddComponent<TextMeshProUGUI>();
        }

        private static void SetField(UnityEngine.Object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"未找到字段 {target.GetType().Name}.{fieldName}。");
            field.SetValue(target, value);
        }

        private static void InvokeLifecycle(KillStreakView view, string methodName)
        {
            typeof(KillStreakView)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(view, null);
        }
    }
}
