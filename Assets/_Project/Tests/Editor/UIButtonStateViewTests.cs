using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Project.Tests.Editor
{
    /// <summary>锁定可复用按钮状态视图同时控制灰态和交互状态。</summary>
    public sealed class UIButtonStateViewTests
    {
        private GameObject _buttonObject;

        [TearDown]
        public void TearDown()
        {
            if (_buttonObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_buttonObject);
            }
        }

        [Test]
        public void SetInteractable_ControlsDisabledVisualAndPointerInteraction()
        {
            Type stateViewType = FindType("UIButtonStateView");
            Assert.That(stateViewType, Is.Not.Null,
                "Production must provide a reusable UIButtonStateView component.");

            _buttonObject = new GameObject(
                "ReleaseButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            Button button = _buttonObject.GetComponent<Button>();
            Component stateView = _buttonObject.AddComponent(stateViewType);
            MethodInfo setInteractable = stateViewType.GetMethod(
                "SetInteractable",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool) },
                null);
            Assert.That(setInteractable, Is.Not.Null,
                "UIButtonStateView must expose public SetInteractable(bool).");

            int clickCount = 0;
            button.onClick.AddListener(() => clickCount++);
            setInteractable.Invoke(stateView, new object[] { false });

            Assert.That(button.interactable, Is.False);
            button.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left
            });
            Assert.That(clickCount, Is.Zero);

            setInteractable.Invoke(stateView, new object[] { true });

            Assert.That(button.interactable, Is.True);
            button.OnPointerClick(new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left
            });
            Assert.That(clickCount, Is.EqualTo(1));
        }

        private static Type FindType(string simpleName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.Name == simpleName)
                    {
                        return type;
                    }
                }
            }

            return null;
        }
    }
}
