using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using _Project.UI.Animations;
using UnityEngine;

namespace _Project.Tests.Editor
{
    public sealed class UIPanelShowHideTests
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
        public void ShowAndHide_StaggerDirectChildrenWithoutChangingLayoutPositions()
        {
            GameObject panelObject = CreateObject("Panel");
            RectTransform panelRect = panelObject.AddComponent<RectTransform>();
            GameObject childObject = CreateObject("Child");
            RectTransform childRect = childObject.AddComponent<RectTransform>();
            childRect.SetParent(panelRect, false);
            childRect.anchoredPosition = new Vector2(24f, -36f);

            GameObject nestedObject = CreateObject("Nested");
            RectTransform nestedRect = nestedObject.AddComponent<RectTransform>();
            nestedRect.SetParent(childRect, false);
            nestedRect.anchoredPosition = new Vector2(8f, -12f);

            UIPanelShowHide panel = panelObject.AddComponent<UIPanelShowHide>();
            Vector2 childPosition = childRect.anchoredPosition;
            Vector2 nestedPosition = nestedRect.anchoredPosition;

            panel.Show();
            DOTween.CompleteAll();

            Assert.That(panel.IsVisible, Is.True);
            Assert.That(childObject.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(childPosition, Is.EqualTo(childRect.anchoredPosition));
            Assert.That(nestedPosition, Is.EqualTo(nestedRect.anchoredPosition));

            panel.Hide();
            DOTween.CompleteAll();

            Assert.That(panel.IsVisible, Is.False);
            Assert.That(childPosition, Is.EqualTo(childRect.anchoredPosition));
            Assert.That(nestedPosition, Is.EqualTo(nestedRect.anchoredPosition));
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new(name, typeof(CanvasGroup));
            _createdObjects.Add(gameObject);
            return gameObject;
        }
    }
}
