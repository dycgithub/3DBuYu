using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using _Project.UI.Animations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Project.Tests.Editor
{
    public sealed class UIButtonEffectTests
    {
        private readonly List<Object> _createdObjects = new();

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
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void PointerDownAndUp_AnimateVisualRootWithoutMovingHitArea()
        {
            GameObject buttonObject = CreateButtonObject();
            UIButtonEffect effect = buttonObject.AddComponent<UIButtonEffect>();
            RectTransform hitArea = buttonObject.GetComponent<RectTransform>();
            Vector3 hitAreaScale = hitArea.localScale;
            Vector2 hitAreaPosition = hitArea.anchoredPosition;

            effect.OnPointerDown(new PointerEventData(EventSystem.current));
            DOTween.CompleteAll();

            Assert.That(effect.IsPressed, Is.True);
            Assert.That(effect.VisualRoot.localScale.y, Is.LessThan(1f));
            Assert.That(hitArea.localScale, Is.EqualTo(hitAreaScale));
            Assert.That(hitArea.anchoredPosition, Is.EqualTo(hitAreaPosition));

            effect.OnPointerUp(new PointerEventData(EventSystem.current));
            DOTween.CompleteAll();

            Assert.That(effect.IsPressed, Is.False);
            Assert.That(effect.VisualRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(effect.VisualRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Disable_RestoresVisualStateAfterInterruptedInteraction()
        {
            GameObject buttonObject = CreateButtonObject();
            UIButtonEffect effect = buttonObject.AddComponent<UIButtonEffect>();

            effect.OnPointerEnter(new PointerEventData(EventSystem.current));
            effect.OnPointerDown(new PointerEventData(EventSystem.current));
            buttonObject.SetActive(false);

            Assert.That(effect.IsPressed, Is.False);
            Assert.That(effect.VisualRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(effect.VisualRoot.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(effect.VisualRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
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
    }
}
