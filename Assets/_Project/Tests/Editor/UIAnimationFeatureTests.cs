using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using _Project.UI.Animations;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证 UI 动效模块开关、立即状态切换和 Inspector 元数据契约。
    /// </summary>
    public sealed class UIAnimationFeatureTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            DOTween.Init();
            DOTween.KillAll();
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void UIButtonEffect_DisabledHoverAndPressAnimationsLeaveVisualStateUntouched()
        {
            GameObject buttonObject = CreateButtonObject();
            UIButtonEffect effect = buttonObject.AddComponent<UIButtonEffect>();
            RectTransform visualRoot = effect.VisualRoot;
            Vector3 baseScale = visualRoot.localScale;
            Vector2 basePosition = visualRoot.anchoredPosition;
            Quaternion baseRotation = visualRoot.localRotation;

            SetField(effect, "_hoverAnimation", false);
            SetField(effect, "_pressAnimation", false);

            effect.OnPointerEnter(new PointerEventData(EventSystem.current));
            effect.OnPointerDown(new PointerEventData(EventSystem.current));
            effect.OnPointerUp(new PointerEventData(EventSystem.current));
            DOTween.CompleteAll();

            Assert.That(visualRoot.localScale, Is.EqualTo(baseScale));
            Assert.That(visualRoot.anchoredPosition, Is.EqualTo(basePosition));
            Assert.That(visualRoot.localRotation, Is.EqualTo(baseRotation));
        }

        [Test]
        public void UIButtonEffect_DisabledRippleDoesNotChangeRippleState()
        {
            GameObject buttonObject = CreateButtonObject();
            GameObject rippleObject = new("RippleLayer", typeof(RectTransform), typeof(CanvasGroup));
            rippleObject.transform.SetParent(buttonObject.transform, false);
            _createdObjects.Add(rippleObject);

            UIButtonEffect effect = buttonObject.AddComponent<UIButtonEffect>();
            RectTransform ripple = rippleObject.GetComponent<RectTransform>();
            ripple.localScale = Vector3.one;
            SetField(effect, "_rippleAnimation", false);

            effect.OnPointerClick(new PointerEventData(EventSystem.current));
            DOTween.CompleteAll();

            Assert.That(ripple.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void UIPanelShowHide_DisabledAnimationModulesApplyStatesImmediately()
        {
            GameObject panelObject = CreatePanelObject();
            panelObject.SetActive(false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(240f, 160f);
            panelRect.anchoredPosition = new Vector2(12f, 24f);

            GameObject childObject = new("Child", typeof(RectTransform), typeof(CanvasGroup));
            childObject.transform.SetParent(panelObject.transform, false);
            _createdObjects.Add(childObject);
            RectTransform childRect = childObject.GetComponent<RectTransform>();
            childRect.anchoredPosition = new Vector2(30f, -18f);
            childRect.localScale = new Vector3(0.8f, 1.2f, 1f);
            CanvasGroup childGroup = childObject.GetComponent<CanvasGroup>();
            childGroup.alpha = 0.65f;

            UIPanelShowHide panel = panelObject.AddComponent<UIPanelShowHide>();
            SetField(panel, "_transformAnimation", false);
            SetField(panel, "_fadeAnimation", false);
            SetField(panel, "_animateChildren", false);
            SetField(panel, "_startHidden", false);
            panelObject.SetActive(true);

            Vector2 homePosition = panel.HomePosition;
            Vector2 childPosition = childRect.anchoredPosition;
            Vector3 childScale = childRect.localScale;
            float childAlpha = childGroup.alpha;
            CanvasGroup panelGroup = panelObject.GetComponent<CanvasGroup>();

            panel.Hide();

            Assert.That(panel.IsVisible, Is.False);
            Assert.That(panelRect.anchoredPosition, Is.Not.EqualTo(homePosition));
            Assert.That(panelGroup.alpha, Is.EqualTo(0f));
            Assert.That(childRect.anchoredPosition, Is.EqualTo(childPosition));
            Assert.That(childRect.localScale, Is.EqualTo(childScale));
            Assert.That(childGroup.alpha, Is.EqualTo(childAlpha));

            panel.Show();

            Assert.That(panel.IsVisible, Is.True);
            Assert.That(panelRect.anchoredPosition, Is.EqualTo(homePosition));
            Assert.That(panelGroup.alpha, Is.EqualTo(1f));
            Assert.That(childRect.anchoredPosition, Is.EqualTo(childPosition));
            Assert.That(childRect.localScale, Is.EqualTo(childScale));
            Assert.That(childGroup.alpha, Is.EqualTo(childAlpha));
        }

        [Test]
        public void AnimationComponents_ExposeTooltipForEverySerializedField()
        {
            Type[] componentTypes =
            {
                typeof(UIButtonEffect),
                typeof(UIPanelShowHide)
            };

            foreach (Type componentType in componentTypes)
            {
                FieldInfo[] fields = componentType.GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields)
                {
                    if (!field.IsDefined(typeof(SerializeField), false))
                        continue;

                    Assert.That(
                        field.IsDefined(typeof(TooltipAttribute), false),
                        Is.True,
                        $"{componentType.Name}.{field.Name} 缺少 Inspector Tooltip。");
                }
            }
        }

        [Test]
        public void AnimationEditors_AreRegisteredForTheirComponents()
        {
            Assert.That(
                typeof(UIButtonEffectEditor).IsDefined(typeof(CustomEditor), false),
                Is.True);
            Assert.That(
                typeof(UIPanelShowHideEditor).IsDefined(typeof(CustomEditor), false),
                Is.True);
        }

        private GameObject CreateButtonObject()
        {
            GameObject buttonObject = new("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            _createdObjects.Add(buttonObject);

            GameObject visualObject = new("VisualRoot", typeof(RectTransform));
            visualObject.transform.SetParent(buttonObject.transform, false);
            _createdObjects.Add(visualObject);

            return buttonObject;
        }

        private GameObject CreatePanelObject()
        {
            GameObject panelObject = new("Panel", typeof(RectTransform), typeof(CanvasGroup));
            _createdObjects.Add(panelObject);
            return panelObject;
        }

        private static void SetField(UnityEngine.Object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"未找到字段 {target.GetType().Name}.{fieldName}。");
            field.SetValue(target, value);
        }
    }
}
